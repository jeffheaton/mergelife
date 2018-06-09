const assert = require('assert')
const ml = require('../mergelife')
const seedrandom = require('seedrandom')

describe('MergeLifeRender', function () {
  it('should produce expected results in case 1', function () {
    const rule = ml.MergeLifeRender.randomRule()
    assert.equal(/([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})/.test(rule), true)
  })

  it('zeros should allocate multi-dim array', function () {
    const render = new ml.MergeLifeRender()
    const a = render.zeros([1, 2, 3])
    assert.equal(1, a.length)
    assert.equal(2, a[0].length)
    assert.equal(3, a[0][0].length)
  })

  it('parseUpdateRule should properly parse', function () {
    const render = new ml.MergeLifeRender()
    const rule = render.parseUpdateRule("a07f-c000-0000-0000-0000-0000-ff80-807f; Conway's Game of Life")
    const expected = [ [ 0, 0, 2, '00', '00', 0, 0 ],
      [ 0, 0, 3, '00', '00', 0, 0 ],
      [ 0, 0, 4, '00', '00', 0, 0 ],
      [ 0, 0, 5, '00', '00', 0, 0 ],
      [ 1024, 1, 7, '80', '7f', 128, 127 ],
      [ 1280, 1, 0, 'a0', '7f', 160, 127 ],
      [ 1536, 0, 1, 'c0', '00', 192, 0 ],
      [ 2048, -1, 6, 'ff', '80', 255, -128 ] ]
    assert.equal(JSON.stringify(expected), JSON.stringify(rule))
  })

  it('randomGrid should produce a random grid', function () {
    seedrandom('42', { global: true })
    const render = new ml.MergeLifeRender()
    const a = render.zeros([10, 10, 3])
    render.randomGrid(a)
    assert.equal(1, a[0][0][0])
    assert.equal(43, a[0][0][1])
    assert.equal(247, a[0][0][2])

    assert.equal(48, a[9][9][0])
    assert.equal(107, a[9][9][1])
    assert.equal(141, a[9][9][2])
  })

  it('calculateModeGrid should average a grid', function () {
    seedrandom('42', { global: true })
    const render = new ml.MergeLifeRender()
    const a = render.zeros([10, 10, 3])
    render.randomGrid(a)
    const b = render.zeros([10, 10])
    render.calculateModeGrid(a, b)
    assert.equal(97, b[0][0])
    assert.equal(197, b[0][9])
    assert.equal(141, b[9][0])
    assert.equal(98, b[9][9])
  })
})
