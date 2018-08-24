const MergeLifeGA = function (config) {
  this.pop = []
  this.children = []
  this.config = config
  this.runCount = 0

  this.topGenome = null
  this.evalCount = 0
  this.lastReport = 0
  this.lastBestScore = 0
  this.noImprovement = 0
  this.totalEvalCount = 0
  this.startTime = 0
  this.requestStop = false
  this.runCount = 1
  this.population = []
  this.startTime = (new Date()).getTime()
  this.newTopGenomeCallback = null
  this.reportCallback = null
  this.foundGenomeCallback = null
  this.evalsPerMin = 0
  this.reportTime = MergeLifeGA.REPORT_TIME

  this.randomize = function (target) {
    target.length = 0
    for (let i = 0; i < this.config.config.populationSize; i++) {
      target.push({
        'score': -10,
        'rule': MergeLifeRender.randomRule(),
        'run': this.runCount
      })
    }
  }

  this.report = function () {
    const now = (new Date()).getTime()
    let shouldReport = false

    this.evalCount++
    this.totalEvalCount++
    if ((now - this.lastReport) > this.reportTime) {
      this.lastReport = now
      shouldReport = true
    }

    if (this.topGenome != null) {
      if (this.topGenome.score > this.lastBestScore) {
        this.lastBestScore = this.topGenome.score
        this.noImprovement = 0
      } else {
        this.noImprovement++
        if (this.noImprovement > this.config.config.patience && !this.requestStop) {
          shouldReport = true
          this.requestStop = true
        }
      }
    }
    if (shouldReport) {
      const elapsed = (now - this.startTime) / 1000.0
      const perSec = this.totalEvalCount / elapsed
      const perMin = Math.floor(perSec * 60.0)
      console.log(`Run #${this.runCount}, Eval #${this.evalCount}: ${JSON.stringify(this.topGenome)}, evals/min=${perMin}`)
      this.evalsPerMin = perMin
      if (this.reportCallback) {
        this.reportCallback()
      }
    }
  }

  this.tournament = function (pop, cycles) {
    let best = null
    for (let i = 0; i < cycles; i++) {
      const idx = Math.floor(Math.random() * (pop.length))
      const challenger = pop[idx]
      this.report()

      if (best != null) {
        if (best.score < challenger.score) {
          best = challenger
          if (this.topGenome == null || best.score > this.topGenome.score) {
            this.topGenome = best
            if (this.newTopGenomeCallback) {
              this.newTopGenomeCallback(this)
            }
          }
        }
      } else {
        best = challenger
      }
    }
    return best
  }

  this.revTournamentIndex = function (population, cycles) {
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

  this.mutate = function (parent) {
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

  this.crossover = function (parent1, parent2) {
    // The genome must be cut at two positions, determine them
    const cutpoint1 = Math.floor(Math.random() * (parent1.length - MergeLifeGA.CUT_LENGTH))
    const cutpoint2 = cutpoint1 + MergeLifeGA.CUT_LENGTH

    // Produce two offspring
    return ([
      parent1.substring(0, cutpoint1) + parent2.substring(cutpoint1, cutpoint2) + parent1.substring(cutpoint2),
      parent2.substring(0, cutpoint1) + parent1.substring(cutpoint1, cutpoint2) + parent2.substring(cutpoint2)
    ])
  }

  this.addScoredChild = function (child) {
    // If this genome belogs to the current run then save it.  (otherwise, it might be
    // a holdover from the last run.)
    if (child.run === this.runCount) {
      if (this.population.length >= this.config.config.populationSize) {
        const idx = this.revTournamentIndex(this.population, this.config.config.evalCycles)
        this.population[idx] = child
      } else {
        this.population.push(child)
      }
    }
  }

  this.evolve = function () {
    if (this.requestStop) {
      // reset and try again
      if (this.topGenome.score >= this.config.config.scoreThreshold) {
        if (this.foundGenomeCallback) {
          this.foundGenomeCallback(this.topGenome)
        }
        // render(this.topGenome.rule)
      }
      this.evalCount = 0
      this.requestStop = false
      this.noImprovement = 0
      this.lastReport = 0
      this.lastBestScore = 0
      this.children.length = 0
      this.population.length = 0
      this.runCount += 1
      this.randomize(this.children)
      this.topGenome = null
      if (this.reportCallback) {
        this.reportCallback()
      }
    } else if (this.children.length === 0) {
      if (Math.random() < this.config.config.crossover) {
        const p1 = this.tournament(this.population, this.config.config.evalCycles)
        const p2 = this.tournament(this.population, this.config.config.evalCycles)
        const c = this.crossover(p1.rule, p2.rule)
        this.children.push({
          'rule': c[0],
          'score': -10,
          'run': this.runCount
        })
        this.children.push({
          'rule': c[1],
          'score': -10,
          'run': this.runCount
        })
      } else {
        const p1 = this.tournament(this.population, this.config.config.evalCycles)
        const c1 = this.mutate(p1.rule)
        this.children.push({
          'rule': c1,
          'score': -10,
          'run': this.runCount
        })
      }
    }
  }
}

const MergeLifeObjective = function (theGrid, theObjective) {
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

  this.objectiveFunctionCycle = function (dump) {
    while (!this.hasStabilized()) {
      this.grid.singleStep()
      const s = this.track()
      if (dump) {
        console.log(JSON.stringify(s))
      }
    }

    return this.scoreCycle()
  }

  this.scoreCycle = function () {
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
      } else if (actual > currentValue.max) {
        // too big
        return accumulator + currentValue.max_weight
      } else {
        let adjust = ((range / 2.0) - Math.abs(actual - ideal)) / (range / 2.0)
        adjust *= currentValue.weight
        return accumulator + adjust
      }
    }, 0)
  }

  this.init = function () {
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

  // The objective
  this.objective = theObjective

  // The grid being evaluated.
  this.grid = theGrid

  // The count of how many CA generations each cell has been the mode.
  this.modeCount = MergeLifeRender.zeros([this.grid.rows, this.grid.cols])

  // The last merged color that each cell was.
  this.lastColor = MergeLifeRender.zeros([this.grid.rows, this.grid.cols])

  // The count of how many CA steps each cell has had its color.
  this.lastColorCount = MergeLifeRender.zeros([this.grid.rows, this.grid.cols])

  // The last CA generation/step that each cell was the merged mode/background.
  this.lastModeStep = MergeLifeRender.zeros([this.grid.rows, this.grid.cols])

  this.init()
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
  exports.MergeLifeEvolve = MergeLifeGA
  exports.maximalRectangle = maximalRectangle
  exports.MergeLifeEvolveUtil = MergeLifeObjective
}

MergeLifeGA.REPORT_TIME = 60000
MergeLifeObjective.CUT_LENGTH = 5
