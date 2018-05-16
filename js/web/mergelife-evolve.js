const MergeLifeEvolve = function (canvas) {

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
