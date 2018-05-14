const assert = require('assert')
const evolve = require('../mergelife-evolve')

describe('largestrect', function () {
  it('should produce expected results in case 1', function () {
    const matrix = [
        [0, 0, 0, 0, 1, 0],
        [0, 0, 1, 0, 0, 1],
        [0, 0, 0, 0, 0, 0],
        [1, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 1],
        [0, 0, 1, 0, 0, 0]
      ]
    assert.equal(12, evolve.maximalRectangle(matrix,0) )
  })
  it('should produce expected results in case 1a', function () {
        const matrix1 = [[1,1],[0,0]]
        const matrix2 = [[0,0],[1,1]]
        const matrix3 = [[1,0],[1,0]]
        const matrix4 = [[0,1],[0,1]]

        assert.equal(2, evolve.maximalRectangle(matrix1,0) )
        assert.equal(2, evolve.maximalRectangle(matrix2,0) )
        assert.equal(2, evolve.maximalRectangle(matrix3,0) )
        assert.equal(2, evolve.maximalRectangle(matrix4,0) )
  })
  it('should produce expected results in case 2', function () {
    const matrix = [
        [0, 0, 0, 0, 1, 0],
        [0, 0, 1, 0, 0, 1],
        [0, 0, 0, 0, 0, 0],
        [1, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 1],
        [0, 0, 1, 0, 0, 0],
        [0, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 0]
      ]
      assert.equal(14, evolve.maximalRectangle(matrix,0) )
  })
  it('should produce expected results in case 2a', function () {
    const matrix1 = [[]]
    const matrix2 = []

    assert.equal(0, evolve.maximalRectangle(matrix1,0) )
    assert.equal(0, evolve.maximalRectangle(matrix2,0) )
  })
  it('should produce expected results in case 3', function () {
    const matrix1 = [
        [0, 0, 0, 0, 1, 0],
        [0, 0, 1, 0, 0, 1],
        [0, 0, 0, 0, 0, 0],
        [1, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 0],
        [0, 0, 1, 0, 0, 1],
        [0, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 0]
      ]
      assert.equal(15, evolve.maximalRectangle(matrix1,0) )
  })
  it('should produce expected results in case 4', function () {
    const matrix1 = [
        [0, 0, 0, 0, 1, 0],
        [0, 0, 0, 0, 0, 0],
        [0, 0, 1, 0, 0, 1],
        [0, 0, 0, 0, 0, 0],
        [1, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 0],
        [0, 0, 1, 0, 0, 1],
        [0, 0, 0, 0, 0, 0],
        [0, 0, 0, 0, 0, 1]
      ]
      assert.equal(16, evolve.maximalRectangle(matrix1,0) )
  })
  it('should produce expected results in case 5', function () {
    const matrix1 = [
        [0, 0, 0, 0, 1, 1, 1],
        [0, 0, 0, 0, 0, 0, 0],
        [0, 0, 0, 1, 1, 1, 1],
        [0, 0, 1, 1, 1, 1, 1],
        [1, 0, 1, 1, 1, 1, 1],
        [1, 0, 1, 1, 1, 1, 1],
        [1, 0, 1, 1, 1, 1, 1]
      ]
      assert.equal(9, evolve.maximalRectangle(matrix1,0) )
  })
})
