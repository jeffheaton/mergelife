// npm run mergelife -- --rows 50 --cols 100 render E542-5F79-9341-F31E-6C6B-7F08-8773-7068

const ml = require('./mergelife')
const commandLineArgs = require('command-line-args')
const commandLineUsage = require('command-line-usage')
const Jimp = require('Jimp')
const fs = require('fs')

function render (ruleText, rows, cols, steps, zoom) {
  const renderer = new ml.MergeLifeRender()
  renderer.init({
    rows: rows,
    cols: cols,
    rule: ruleText
  })
  for (let i = 0; i < steps; i++) {
    renderer.singleStep()
  }

  const imageData = renderer.lattice[0]

  const image = new Jimp(cols * zoom, rows * zoom, function (err, image) {
    if (err) throw err

    imageData.forEach((row, y) => {
      row.forEach((color, x) => {
        color = (color[2] << 8) + (color[1] << 16) + (color[0] << 24) + 0xff
        const x2 = x * zoom
        const y2 = y * zoom
        for (let i = 0; i < zoom; i++) {
          for (let j = 0; j < zoom; j++) {
            image.setPixelColor(color, x2 + i, y2 + j)
          }
        }
      })
    })
  })

  image.write(`${ruleText}.png`, (err) => {
    if (err) throw err
  })
}

const optionDefinitions = [
  {
    name: 'help',
    alias: 'h',
    type: Boolean,
    description: 'Display this usage guide.'
  },
  { name: 'rows', type: Number, description: 'Number of rows in the grid.' },
  { name: 'cols', type: Number, description: 'Number of columns in the grid.' },
  { name: 'config', type: String, description: 'Location of the config file.' },
  { name: 'renderSteps', type: String, description: 'The number of steps to use when rendering.' },
  { name: 'zoom', type: String, description: 'How many display pixels per cell.' },
  { name: 'command', type: String, multiple: true, defaultOption: true }
]

const options = commandLineArgs(optionDefinitions)

const config = options.config
  ? JSON.parse(fs.readFileSync(options.config, 'utf8'))
  : {config: {}}

const rows = options.rows || config.config.rows || 100
const cols = options.cols || config.config.cols || 100
const zoom = options.zoom || config.config.zoom || 5
const renderSteps = options.renderSteps || config.renderStep || 250

if (options.help) {
  const usage = commandLineUsage([
    {
      header: 'Typical Example',
      content: 'mergelife-util --rows 100 --cols 100 render 6769-5dd6-7d03-564e-a5ec-cae2-54c4-810c'
    },
    {
      header: 'Options',
      optionList: optionDefinitions
    }
  ])
  console.log(usage)
} else if (options.command[0] === 'render') {
  if (options.command.length < 2) {
    console.log('Must specify what rule hex-code you wish to render.')
    process.exit(1)
  } else {
    const rule = options.command[1]
    render(rule, rows, cols, renderSteps, zoom)
  }
} else {
  console.log(`Unknown command ${options.command[0]}`)
}
