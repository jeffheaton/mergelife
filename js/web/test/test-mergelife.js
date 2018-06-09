const assert = require('assert')
const ml = require('../mergelife')

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
})
