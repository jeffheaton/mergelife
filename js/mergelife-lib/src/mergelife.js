const MergeLifeRender = function () {
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
    const grid2 = grid || this.grid[this.currentGrid]
    for (let row = 0; row < grid2.length; row += 1) {
      const line = grid2[row]
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
      for (let col = 0; col < lineIn.length; col += 1) {
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
      this.grid[this.currentGrid],
      this.grid[this.currentGrid === 0 ? 1 : 0],
      this.updateRule)
    this.currentGrid = this.currentGrid === 0 ? 1 : 0
  }

  this.animateFunction = function () {
    if (this.autoStep) {
      this.singleStep()
    }
    this.render(0, this.grid[this.currentGrid])
  }

  this.stopAnimation = function () {
    clearInterval(this.updateEvent)
    this.updateEvent = null
  }

  this.startAnimation = function (time) {
    this.stepCount = 0
    this.randomGrid(this.grid[this.currentGrid])
    if (this.updateEvent === null) {
      this.updateEvent = setInterval(() => this.animateFunction(), time || 10)
    }
  }

  this.render = function () {
    const grid = this.grid[this.currentGrid]

    this.ctx.strokeStyle = 'grey'

    const wid = grid[0].length
    const hei = grid.length
    const imgdata = this.ctx.getImageData(0, 0, wid, hei)
    const pix = imgdata.data

    const tw = wid * 4
    for (let row = 0; row < grid.length; row += 1) {
      for (let col = 0; col < grid[0].length; col += 1) {
        const pos = (tw * row) + (col * 4)
        const pixel = (this.colorRangeLow == null) ? this.getColor(grid[row][col])
          : this.getColorRange(grid[row][col])
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

    if (this.mouseInside && this.controlsOn) {
      this.renderControls()
    }
    if (this.postRenderFunction) {
      this.postRenderFunction(this.ctx)
    }
  }

  this.renderButton = function (idx) {
    const x = 5 + (35 * idx)
    const y = this.ctx.canvas.height - 40

    this.ctx.globalAlpha = 0.8
    this.ctx.fillStyle = '#a0a0a0'
    this.ctx.fillRect(x, y, 32, 32)
    this.ctx.lineWidth = 2
    this.ctx.fillStyle = '#606060'
    this.ctx.strokeStyle = '#606060'
    this.ctx.strokeRect(x, y, 32, 32)
    this.ctx.globalAlpha = 1.0
    return [x, y]
  }

  this.renderStopButton = function (idx) {
    const r = this.renderButton(idx)
    const x = r[0]
    const y = r[1]
    this.ctx.fillRect(x + 8, y + 8, 16, 16)
  }

  this.renderPauseButton = function (idx) {
    const r = this.renderButton(idx)
    const x = r[0]
    const y = r[1]
    this.ctx.fillRect(x + 8, y + 8, 6, 16)
    this.ctx.fillRect(x + 18, y + 8, 6, 16)
  }

  this.renderPlayButton = function (idx) {
    const r = this.renderButton(idx)
    const x = r[0]
    const y = r[1]
    this.ctx.beginPath()
    this.ctx.moveTo(x + 10, y + 8)
    this.ctx.lineTo(x + 10, y + 24)
    this.ctx.lineTo(x + 22, y + 15)
    this.ctx.closePath()
    this.ctx.fill()
  }

  this.renderClearButton = function (idx) {
    const r = this.renderButton(idx)
    const x = r[0]
    const y = r[1]
    this.ctx.strokeStyle = 'black'
    this.ctx.beginPath()
    this.ctx.moveTo(x + 8, y + 8)
    this.ctx.lineTo(x + 24, y + 24)
    this.ctx.closePath()
    this.ctx.stroke()
  }

  this.renderStepButton = function (idx) {
    const r = this.renderButton(idx)
    const x = r[0]
    const y = r[1]
    this.ctx.beginPath()
    this.ctx.moveTo(x + 10, y + 8)
    this.ctx.lineTo(x + 10, y + 24)
    this.ctx.lineTo(x + 22, y + 15)
    this.ctx.closePath()
    this.ctx.fill()
    this.ctx.fillRect(x + 20, y + 8, 4, 16)
  }

  this.renderControls = function () {
    this.ctx.font = 'bold 20px Georgia'
    const text = this.stepCount.toLocaleString() // parseInt()
    const lineHeight = this.ctx.measureText('M').width
    const textY = lineHeight
    const textX = this.ctx.canvas.width - (this.ctx.measureText(text).width + 10)
    this.ctx.fillStyle = '#ffffff'
    this.ctx.strokeStyle = 'black'
    this.ctx.lineWidth = 7
    this.ctx.strokeText(text, textX, textY)
    this.ctx.fillText(text, textX, textY)

    if (this.autoStep) {
      this.renderPauseButton(0)
    } else {
      this.renderPlayButton(0)
    }
    this.renderStepButton(1)
    this.renderStopButton(2)
  }

  this.mouseEnter = function () {
    this.mouseInside = true
  }

  this.mouseExit = function () {
    this.mouseInside = false
  }

  this.mouseUp = function (event) {
    const x = event.layerX
    const y = event.layerY

    const buttonTop = this.ctx.canvas.height - 40
    const buttonBottom = buttonTop + 32

    if (y >= buttonTop && y < buttonBottom && this.controlsOn) {
      const buttonIdx = Math.floor((x - 5) / 35)
      switch (buttonIdx) {
        case 0:
          this.autoStep = !this.autoStep
          break
        case 1:
          this.singleStep()
          break
        case 2:
          this.randomGrid(this.grid[this.currentGrid])
          this.autoStep = false
          break
      }
    }
  }

  this.init = function (params) {
    this.rows = 0
    this.cols = 0
    this.gridMode = 0

    if (params.canvas == null) {
      this.rows = params.rows || 100
      this.cols = params.cols || 100
      this.autoStep = false
      this.controlsOn = false
    } else {
      this.ctx = params.canvas.getContext('2d')
      this.cellSize = params.cellSize || 5
      this.rows = this.ctx.canvas.height / this.cellSize
      this.cols = this.ctx.canvas.width / this.cellSize
      this.ctx.canvas.addEventListener('mouseenter', () => this.mouseEnter())
      this.ctx.canvas.addEventListener('mouseout', () => this.mouseExit())
      this.ctx.canvas.addEventListener('mouseup', (e) => this.mouseUp(e))
      this.autoStep = true
      this.controlsOn = params.controls === true
    }
    
    this.grid = []
    this.grid[0] = MergeLifeRender.zeros([this.rows, this.cols, 3])
    this.grid[1] = MergeLifeRender.zeros([this.rows, this.cols, 3])
    this.mergeGrid = MergeLifeRender.zeros([this.rows, this.cols, 1])
    this.currentGrid = 0
    this.updateRule = null
    this.updateEvent = null
    this.stepCount = 0

    this.randomGrid(this.grid[0])

    const result = this.parseUpdateRule(params.rule)
    this.updateRule = result
  }

  this.postRenderFunction = null
  this.colorRangeLow = null
  this.colorRangeHigh = null
  this.mouseInside = false
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

MergeLifeRender.zeros = function (dimensions) {
  const array = []

  for (let i = 0; i < dimensions[0]; ++i) {
    array.push(dimensions.length === 1 ? 0 : MergeLifeRender.zeros(dimensions.slice(1)))
  }
  return array
}

if (typeof module !== 'undefined' && typeof module.exports !== 'undefined') {
  exports.MergeLifeRender = MergeLifeRender
}
