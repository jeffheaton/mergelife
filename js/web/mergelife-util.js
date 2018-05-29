// npm run mergelife -- --rows 50 --cols 100 render E542-5F79-9341-F31E-6C6B-7F08-8773-7068

const ml = require('./mergelife')
const mlev = require('./mergelife-evolve')
const commandLineArgs = require('command-line-args')
const commandLineUsage = require('command-line-usage')
const Jimp = require('Jimp')
const fs = require('fs')
const cluster = require('cluster')
const os = require('os')
const {performance} = require('perf_hooks')

let topGenome = null
let evalCount = 0
let lastReport = 0
let lastBestScore = 0
let noImprovement = 0
let totalEvalCount = 0
let startTime = 0
let requestStop = false
let runCount = 0
const REPORT_TIME = 60000
const CUT_LENGTH = 5

function objectiveFunction (dump, ruleText) {
  let sum = 0
  for (let i = 0; i < 5; i++) {
    const renderer = new ml.MergeLifeRender()
    renderer.init({
      rows: config.config.rows,
      cols: config.config.cols,
      rule: ruleText
    })

    if (dump) {
      console.log(`Cycle #${i}`)
    }

    const tracker = new mlev.MergeLifeEvolve(renderer, objective)
    const score = tracker.objectiveFunctionCycle(dump)
    sum += score
  }
  return sum / 5.0
}

function score (ruleText, objective) {
  const score = objectiveFunction(true, ruleText)
  console.log(`Final score: ${score}`)
}

function render (ruleText) {
  const rows = config.config.rows
  const cols = config.config.cols
  const steps = config.config.renderSteps
  const zoom = config.config.zoom

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

  const filename = `${ruleText}.png`

  image.write(filename, (err) => {
    if (err) {
      throw err
    } else {
      console.log(`Saved: ${filename}`)
    }
  })
}

function report () {
  const now = performance.now()
  let report = false

  evalCount++
  totalEvalCount++
  if ((now - lastReport) > REPORT_TIME) {
    lastReport = now
    report = true
  }

  if (topGenome != null) {
    if (topGenome.score > lastBestScore) {
      lastBestScore = topGenome.score
      noImprovement = 0
    } else {
      noImprovement++
      if (noImprovement > config.config.patience && !requestStop) {
        report = true
        requestStop = true
      }
    }
  }
  if (report) {
    const elapsed = (now - startTime) / 1000.0
    console.log(elapsed)
    console.log(totalEvalCount)
    const perSec = totalEvalCount / elapsed
    const perMin = Math.floor(perSec * 60.0)
    console.log(`Run #${runCount}, Eval #${evalCount}: ${JSON.stringify(topGenome)}, evals/min=${perMin}`)
  }
}

function tournament (population, cycles) {
  let best = null
  for (let i = 0; i < cycles; i++) {
    const idx = Math.floor(Math.random() * (population.length))
    const challenger = population[idx]
    report()

    if (best != null) {
      if (best.score < challenger.score) {
        best = challenger
        if (topGenome == null || best.score > topGenome.score) {
          topGenome = best
        }
      }
    } else {
      best = challenger
    }
  }

  return best
}

function revTournamentIndex (population, cycles) {
  let worst = null
  let worstIdx = null

  for (let i = 0; i < cycles; i++) {
    const idx = Math.floor(Math.random() * (population.length))
    const challenger = population[idx]

    if (worst != null) {
      if (worst.score > challenger.score) {
        worst = challenger
        worstIdx = idx
      }
    } else {
      worst = challenger
      worstIdx = idx
    }
  }

  return worstIdx
}

function mutate (parent) {
  const h = '0123456789abcdef'
  let result = ''

  let done = false
  while (!done) {
    const i = Math.floor(Math.random() * parent.length)

    if (parent.charAt(i) !== '-') {
      const i2 = Math.floor(Math.random() * h.length)
      result = parent.substring(0, i) + h.charAt(i2) + parent.substring(i + 1)
      result = result.toLowerCase()
      if (result.toLowerCase() !== parent.toLowerCase()) {
        done = true
      }
    }
  }
  return result
}

function crossover (parent1, parent2) {
  // The genome must be cut at two positions, determine them
  const cutpoint1 = Math.floor(Math.random() * (parent1.length - CUT_LENGTH))
  const cutpoint2 = cutpoint1 + CUT_LENGTH

  // Produce two offspring
  return ([
    parent1.substring(0, cutpoint1) + parent2.substring(cutpoint1, cutpoint2) + parent1.substring(cutpoint2),
    parent2.substring(0, cutpoint1) + parent1.substring(cutpoint1, cutpoint2) + parent2.substring(cutpoint2)
  ])
}

function randomize (pop) {
  pop.length = 0
  for (let i = 0; i < config.config.populationSize; i++) {
    pop.push({'score': -10, 'rule': ml.MergeLifeRender.randomRule(), 'run': runCount})
  }
}

function evolve () {
  if (cluster.isMaster) {
    const cpus = os.cpus().length
    startTime = performance.now()

    console.log(`Forking for ${cpus} CPUs`)
    for (let i = 0; i < cpus; i++) {
      cluster.fork()
    }

    const children = []
    randomize(children)
    const population = []

    cluster.on('message', (worker, message, handle) => {
      if (arguments.length === 2) {
        handle = message
        message = worker
        worker = undefined
      }

      // If this genome belogs to the current run then save it.  (it might be
      // a holdover from the last run.)
      if (message.run === runCount) {
        if (population.length >= config.config.populationSize) {
          const idx = revTournamentIndex(population, config.config.evalCycles)
          population[idx] = message
        } else {
          population.push(message)
        }
      }

      if (requestStop) {
        // reset and try again
        if (topGenome.score >= config.config.scoreThreshold) {
          render(topGenome.rule)
        }
        evalCount = 0
        requestStop = false
        noImprovement = 0
        lastReport = 0
        lastBestScore = 0
        children.length = 0
        population.length = 0
        runCount += 1
        randomize(children)
        topGenome = null
        worker.send(children.pop())
      } else if (children.length > 0) {
        worker.send(children.pop())
      } else {
        if (Math.random() < config.config.crossover) {
          const p1 = tournament(population, config.config.evalCycles)
          const p2 = tournament(population, config.config.evalCycles)
          const c = crossover(p1.rule, p2.rule)
          children.push({'rule': c[0], 'score': -10, 'run': runCount})
          children.push({'rule': c[1], 'score': -10, 'run': runCount})
        } else {
          const p1 = tournament(population, config.config.evalCycles)
          const c1 = mutate(p1.rule)
          children.push({'rule': c1, 'score': -10, 'run': runCount})
        }
        worker.send(children.pop())
        totalEvalCount += 1
      }
    })

    cluster.on('online', (worker) => {
      if (children.length > 0) {
        worker.send(children.pop())
        totalEvalCount += 1
      }
    })
  } else {
    process.on('message', genome => {
      const score = objectiveFunction(false, genome.rule)
      process.send({'rule': genome.rule, 'score': score, 'run': genome.run})
    })
  }
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

config.config.rows = options.rows || config.config.rows || 100
config.config.cols = options.cols || config.config.cols || 100
config.config.zoom = options.zoom || config.config.zoom || 5
config.config.renderSteps = options.renderSteps || config.renderStep || 250
const objective = config.objective

if (!('command' in options) || options.help) {
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
    render(rule)
  }
} else if (options.command[0] === 'score') {
  if (options.command.length < 2) {
    console.log('Must specify what rule hex-code you wish to score.')
    process.exit(1)
  } else if (!objective) {
    console.log('Must specify objective function (from config file).')
    process.exit(1)
  } else {
    const rule = options.command[1]
    score(rule, objective)
  }
} else if (options.command[0] === 'evolve') {
  evolve()
} else {
  console.log(`Unknown command ${options.command[0]}`)
}
