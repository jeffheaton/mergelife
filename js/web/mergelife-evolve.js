const MergeLifeEvolve = function (theGrid) {
  this.alloc = function (rows, cols, depth) {
    const result = []

    for (let row = 0; row < rows; row += 1) {
      const temp = []
      for (let col = 0; col < cols; col += 1) {
        if (depth === 1) {
          temp[col] = 0
        } else {
          const temp2 = []
          for (let d = 0; d < depth; d++) {
            temp2[d] = 0
          }
          temp[col] = temp2
        }
      }
      result[row] = temp
    }

    return result
  }

  this.track = function () {
    const height = this.grid.rows
    const width = this.grid.cols
    const size = height * width

    if (this.currentStats.statMode === this.grid.gridMode) {
      const age = this.currentStats.statModeAge + 1
      this.currentStats.statModeAge = age
    } else {
      this.currentStats.statMode = this.grid.gridMode
      this.currentStats.statModeAge = 0
    }

    // What percent of the grid is the mode, what percent is the background
    let mc = 0
    let sameColor = 0
    let act = 0

    for (let row = 0; row < this.grid.rows; row++) {
      for (let col = 0; col < this.grid.cols; col++) {
        if (this.grid.lattice[this.grid.currentGrid][row][col] === this.grid.gridMode) {
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
          this.grid.mergeGrid[row][col] === this.grid.modeGrid) {
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

    const rect = maximalRectangle(this.grid.mergeGrid, this.grid.modeGrid)

    this.currentStats.statModeCount = mc
    this.currentStats.statBackground = mc / size
    this.currentStats.statForeground = sameColor / size
    this.currentStats.statActive = act / size
    this.currentStats.statCHaos = cntChaos / size
    this.currentStats.statSteps = this.grid.stepCount
    this.currentStats.statRect = rect / size

    return this.currentStats
  }

  // The grid being evaluated.
  this.grid = theGrid

  // The count of how many CA generations each cell has been the mode.
  this.modeCount = this.alloc([this.grid.rows, this.grid.cols])

  // The last merged color that each cell was.
  this.lastColor = this.alloc([this.grid.rows, this.grid.cols])

  // The count of how many CA steps each cell has had its color.
  this.lastColorCount = this.alloc([this.grid.rows, this.grid.cols])

  // The last CA generation/step that each cell was the merged mode/background.
  this.lastModeStep = this.alloc([this.grid.rows, this.grid.cols])

  // The last count of merged mode (background) cells.
  this.lastModeCount = 0

  // How many CA generations has the merged mode count been the same.
  this.modeCountSame = 0

  // The current evaluation stats.
  this.currentStats = {}

  this.currentStats.statModeAge = 0.0
  this.currentStats.statMode = 0.0
  this.currentStats.statModeCount = 0.0
  this.currentStats.statBackground = 0.0
  this.currentStats.statForeground = 0.0
  this.currentStats.statActive = 0.0
  this.currentStats.statChaos = 0.0
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
