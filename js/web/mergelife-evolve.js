const MergeLifeEvolve = function (theGrid, theObjective) {
  this.zeros = function (dimensions) {
    const array = []

    for (let i = 0; i < dimensions[0]; ++i) {
      array.push(dimensions.length === 1 ? 0 : this.zeros(dimensions.slice(1)))
    }
    return array
  }

  this.track = function () {
    const height = this.grid.rows
    const width = this.grid.cols
    const size = height * width

    if (this.currentStats.mode === this.grid.gridMode) {
      const age = this.currentStats.mage + 1
      this.currentStats.mage = age
    } else {
      this.currentStats.mode = this.grid.gridMode
      this.currentStats.mage = 0
    }

    // What percent of the grid is the mode, what percent is the background
    let mc = 0
    let sameColor = 0
    let act = 0

    for (let row = 0; row < this.grid.rows; row++) {
      for (let col = 0; col < this.grid.cols; col++) {
        if (this.grid.mergeGrid[row][col] === this.grid.gridMode) {
          this.modeCount[row][col]++
          this.lastModeStep[row][col] = this.grid.stepCount
          if (this.modeCount[row][col] > 50) {
            mc++
          }
        } else {
          this.modeCount[row][col] = 0
        }

        const sinceMode = this.grid.stepCount - this.lastModeStep[row][col]
        if (this.grid.stepCount > 25 && (sinceMode > 5 && sinceMode < 25)) {
          act++
        }

        if (this.lastColor[row][col] !== this.grid.mergeGrid[row][col] ||
          this.grid.mergeGrid[row][col] === this.grid.gridMode) {
          this.lastColor[row][col] = this.grid.mergeGrid[row][col]
          this.lastColorCount[row][col] = 0
        } else {
          this.lastColorCount[row][col]++
          if (this.lastColorCount[row][col] > 5) {
            sameColor++
          }
        }
      }
    }

    const cntChaos = size - (mc + sameColor + act)

    const rect = maximalRectangle(this.grid.mergeGrid, this.grid.gridMode)

    this.currentStats.mc = mc
    this.currentStats.background = mc / size
    this.currentStats.foreground = sameColor / size
    this.currentStats.active = act / size
    this.currentStats.chaos = cntChaos / size
    this.currentStats.steps = this.grid.stepCount
    this.currentStats.rect = rect / size

    return this.currentStats
  }

  this.hasStabilized = function () {
    // Time to stop?
    const mc = this.currentStats.mc
    if (mc === this.lastModeCount) {
      this.modeCountSame++
      if (this.modeCountSame > 100) {
        return true
      }
    } else {
      this.modeCountSame = 0
      this.lastModeCount = mc
    }
    return this.grid.stepCount > 1000
  }

  this.objectiveFunction = function (dump) {
    while (!this.hasStabilized()) {
      this.grid.singleStep()
      const s = this.track()
      if (dump) {
        console.log(JSON.stringify(s))
      }
    }

    return this.objective.reduce((accumulator, currentValue) => {
      const range = currentValue.max - currentValue.min
      const ideal = range / 2

      if (!(currentValue.stat in this.currentStats)) {
        console.log(`Unknown objective stat: ${currentValue.stat}`)
      }

      const actual = this.currentStats[currentValue.stat]
      if (actual < currentValue.min) {
        // too small
        return accumulator + currentValue.min_weight
      } else if (actual > this.max) {
        // too big
        return accumulator + currentValue.max_weight
      } else {
        let adjust = ((range / 2) - Math.abs(actual - ideal)) / (range / 2)
        adjust *= currentValue.weight
        return accumulator + adjust
      }
    }, 0)
  }

  // The objective
  this.objective = theObjective

  // The grid being evaluated.
  this.grid = theGrid

  // The count of how many CA generations each cell has been the mode.
  this.modeCount = this.zeros([this.grid.rows, this.grid.cols])

  // The last merged color that each cell was.
  this.lastColor = this.zeros([this.grid.rows, this.grid.cols])

  // The count of how many CA steps each cell has had its color.
  this.lastColorCount = this.zeros([this.grid.rows, this.grid.cols])

  // The last CA generation/step that each cell was the merged mode/background.
  this.lastModeStep = this.zeros([this.grid.rows, this.grid.cols])

  // The last count of merged mode (background) cells.
  this.lastModeCount = 0

  // How many CA generations has the merged mode count been the same.
  this.modeCountSame = 0

  // The current evaluation stats.
  this.currentStats = {}

  this.currentStats.mage = 0.0
  this.currentStats.mode = 0.0
  this.currentStats.mc = 0
  this.currentStats.background = 0.0
  this.currentStats.foreground = 0.0
  this.currentStats.active = 0.0
  this.currentStats.chaos = 0.0
}

function maxAreaInHist (height) {
  const stack = []
  let i = 0
  let max = 0

  while (i < height.length) {
    if (stack.length === 0 || height[stack[stack.length - 1]] <= height[i]) {
      stack.push(i++)
    } else {
      const t = stack.pop()
      max = Math.max(max, height[t] *
        (stack.length === 0 ? i : i - stack[stack.length - 1] - 1))
    }
  }

  return max
}

function maximalRectangle (matrix, background) {
  const m = matrix.length
  const n = (m === 0) ? 0 : matrix[0].length
  const height = []

  let maxArea = 0
  for (let i = 0; i < m; i++) {
    height.push([])
    height[i][n] = 0
    for (let j = 0; j < n; j++) {
      if (matrix[i][j] !== background) {
        height[i][j] = 0
      } else {
        height[i][j] = i === 0 ? 1 : height[i - 1][j] + 1
      }
    }
  }

  for (let i = 0; i < m; i++) {
    const area = maxAreaInHist(height[i])
    if (area > maxArea) {
      maxArea = area
    }
  }

  return maxArea
}

if (typeof module !== 'undefined' && typeof module.exports !== 'undefined') {
  exports.MergeLifeEvolve = MergeLifeEvolve
  exports.maximalRectangle = maximalRectangle
}
