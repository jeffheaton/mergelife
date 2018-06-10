const MergeLifeRender = function () {
  this.zeros = function (dimensions) {
    const array = []

    for (let i = 0; i < dimensions[0]; ++i) {
      array.push(dimensions.length === 1 ? 0 : this.zeros(dimensions.slice(1)))
    }
    return array
  }

  this.parseUpdateRule = function (hexCode) {
    const i = hexCode.indexOf(';')
    if (i !== -1) {
      hexCode = hexCode.substring(0, i)
    }
    hexCode = hexCode.replace(/\W+/g, '')
    const result = []
    for (let i = 0; i < 8; i++) {
      const i2 = i * 4
      const o1 = hexCode.substr(i2, 2)
      const o2 = hexCode.substr(i2 + 2, 2)
      const o1d = parseInt(o1, 16)
      let o2d = parseInt(o2, 16)
      if ((o2d & 0x80) > 0) {
        o2d = o2d - 0x100
      }

      const percent = (o2d > 0) ? o2d / 127.0 : o2d / 128.0

      let range = o1d * 8
      if (range >= 2040) {
        range = 2048
      }

      result.push([range, percent, i, o1, o2, o1d, o2d])
    }

    result.sort(function (a, b) {
      return a[0] - b[0]
    })
    return result
  }

  this.randomGrid = function (grid) {
    for (let row = 0; row < grid.length; row += 1) {
      const line = grid[row]
      for (let col = 0; col < line.length; col += 1) {
        const pixel = line[col]
        for (let i = 0; i < 3; i++) {
          pixel[i] = Math.floor(Math.random() * 256)
        }
      }
    }
  }

  this.calculateModeGrid = function (gridIn, gridOut) {
    const freq = Array.apply(null, Array(256)).map(Number.prototype.valueOf, 0)

    for (let row = 0; row < gridIn.length; row += 1) {
      const lineIn = gridIn[row]
      const lineOut = gridOut[row]
      for (let col = 0; col < gridIn.length; col += 1) {
        const pixel = lineIn[col]
        let sum = 0
        for (let i = 0; i < 3; i++) {
          sum += pixel[i]
        }
        const v = Math.floor(sum / 3)
        freq[v] += 1
        lineOut[col] = v
      }
    }

    return freq.indexOf(Math.max(...freq))
  }

  this.sumNeighbor = function (grid, row, col, pad) {
    const colTransform = [0, 0, -1, 1, -1, 1, 1, -1]
    const rowTransform = [-1, 1, 0, 0, -1, 1, -1, 1]

    let sum = 0
    for (let i = 0; i < colTransform.length; i++) {
      const neighborRow = rowTransform[i] + row
      const neighborCol = colTransform[i] + col
      if (neighborRow >= 0 && neighborCol >= 0 && neighborRow < grid.length && neighborCol < grid[0].length) {
        sum += grid[neighborRow][neighborCol]
      } else {
        sum += pad
      }
    }
    return sum
  }

  this.updateStep = function (grid, gridNext, updateRule) {
    this.gridMode = this.calculateModeGrid(grid, this.mergeGrid)

    for (let row = 0; row < grid.length; row += 1) {
      const line = grid[row]
      const lineNext = gridNext[row]
      for (let col = 0; col < line.length; col += 1) {
        const nc = this.sumNeighbor(this.mergeGrid, row, col, this.gridMode)

        for (let i = 0; i < updateRule.length; i++) {
          if (nc < updateRule[i][0]) {
            let ki = updateRule[i][2]
            let pct = updateRule[i][1]

            if (pct < 0) {
              pct = Math.abs(pct)
              ki += 1
              if (ki >= updateRule.length) {
                ki = 0
              }
            }

            for (let j = 0; j < 3; j++) {
              let d = MergeLifeRender.prototype.KEY_COLOR[ki][j] - grid[row][col][j]
              d *= pct
              d = Math.floor(d)
              lineNext[col][j] = line[col][j] + d
            }
            break
          }
        }
      }
    }
  }

  this.getColor = function (pixel) {
    return [pixel[0], pixel[1], pixel[2]]
  }

  this.ccomp = function (c1, c2, p) {
    const high = Math.max(c1, c2)
    const low = Math.min(c1, c2)
    return Math.floor(((high - low) * (p / 255.0)) + low)
  }

  this.getColorRange = function (pixel) {
    const r = this.ccomp(this.colorRangeLow[0], this.colorRangeHigh[0], pixel[0])
    const g = this.ccomp(this.colorRangeLow[1], this.colorRangeHigh[1], pixel[1])
    const b = this.ccomp(this.colorRangeLow[2], this.colorRangeHigh[2], pixel[2])
    return [r, g, b]
  }

  this.singleStep = function () {
    this.stepCount += 1

    this.updateStep(
      this.lattice[this.currentGrid],
      this.lattice[this.currentGrid === 0 ? 1 : 0],
      this.update_rule)
    this.currentGrid = this.currentGrid === 0 ? 1 : 0
  }

  this.animateFunction = function () {
    this.singleStep()
    const lattice = this.lattice[this.currentGrid]
    this.render(0, lattice)
  }

  this.stopAnimation = function () {
    clearInterval(this.updateEvent)
  }

  this.startAnimation = function (time) {
    this.stepCount = 0
    this.randomGrid(this.lattice[this.currentGrid])
    if (this.updateEvent === null) {
      this.updateEvent = setInterval(() => this.animateFunction(), time || 10)
    }
  }

  this.render = function () {
    const lattice = this.lattice[this.currentGrid]

    this.ctx.strokeStyle = 'grey'

    const wid = lattice[0].length
    const hei = lattice.length
    const imgdata = this.ctx.getImageData(0, 0, wid, hei)
    const pix = imgdata.data

    const tw = wid * 4
    for (let row = 0; row < lattice.length; row += 1) {
      for (let col = 0; col < lattice[0].length; col += 1) {
        const pos = (tw * row) + (col * 4)
        const pixel = (this.colorRangeLow == null) ? this.getColor(lattice[row][col])
          : this.getColorRange(lattice[row][col])
        pix[pos] = pixel[0]
        pix[pos + 1] = pixel[1]
        pix[pos + 2] = pixel[2]
        pix[pos + 3] = 255
      }
    }

    const newCanvas = $('<canvas>')
      .attr('width', imgdata.width)
      .attr('height', imgdata.height)[0]

    newCanvas.getContext('2d').putImageData(imgdata, 0, 0)

    this.ctx.save()
    this.ctx.scale(this.cellSize, this.cellSize)
    this.ctx.drawImage(newCanvas, 0, 0)
    this.ctx.restore()
    this.renderControls()
    this.postRenderFunction(this.ctx)
  }

  this.renderControls = function () {
    this.ctx.font = 'bold 20px Georgia'
    const lineHeight = this.ctx.measureText('M').height
    const textY = lineHeight
    const textX = this.ctx.canvas.height
    this.ctx.fillStyle = '#ffffff'
    this.ctx.strokeStyle = 'black'
    this.ctx.lineWidth = 7
    this.ctx.strokeText(this.stepCount, textX, textY)
    this.ctx.fillText(this.stepCount, textX, textY)

    /* const text = 'Hello world!'
    const blur = 5
    const width = this.ctx.measureText(text).width + blur * 2
    this.ctx.font = '20px Georgia'
    this.ctx.textBaseline = 'top'
    this.ctx.shadowColor = '#000'
    this.ctx.shadowOffsetX = width
    this.ctx.shadowOffsetY = 0
    this.ctx.shadowBlur = blur
    this.ctx.fillText(text, -width, 0)
    this.ctx.shadowBlur = 0
    this.ctx.fillStyle = ''
    this.ctx.fillText(text, -width, 0)*/
  }

  this.init = function (params) {
    this.rows = 0
    this.cols = 0
    this.gridMode = 0

    if (params.canvas == null) {
      this.rows = params.rows || 100
      this.cols = params.cols || 100
    } else {
      this.ctx = params.canvas.getContext('2d')
      this.cellSize = params.cellSize || 5
      this.rows = this.ctx.canvas.height / this.cellSize
      this.cols = this.ctx.canvas.width / this.cellSize
    }

    this.lattice = []
    this.lattice[0] = this.zeros([this.rows, this.cols, 3])
    this.lattice[1] = this.zeros([this.rows, this.cols, 3])
    this.mergeGrid = this.zeros([this.rows, this.cols, 1])
    this.currentGrid = 0
    this.update_rule = null
    this.updateEvent = null
    this.stepCount = 0

    this.randomGrid(this.lattice[0])

    const result = this.parseUpdateRule(params.rule)
    this.update_rule = result
  }

  this.postRenderFunction = null
  this.colorRangeLow = null
  this.colorRangeHigh = null
}

MergeLifeRender.prototype.KEY_COLOR = [
  [0, 0, 0], // Black 0
  [255, 0, 0], // Red 1
  [0, 255, 0], // Green 2
  [255, 255, 0], // Yellow 3
  [0, 0, 255], // Blue 4
  [255, 0, 255], // Purple 5
  [0, 255, 255], // Cyan 6
  [255, 255, 255] // White 7
]

MergeLifeRender.randomRule = function () {
  let str = ''

  for (let i = 0; i < 8; i++) {
    let r = Math.floor(Math.random() * 0x10000).toString(16)
    while (r.length < 4) {
      r = '0' + r
    }
    if (str.length > 0) {
      str += '-'
    }

    str += r
  }
  return str
}

if (typeof module !== 'undefined' && typeof module.exports !== 'undefined') {
  exports.MergeLifeRender = MergeLifeRender
}
