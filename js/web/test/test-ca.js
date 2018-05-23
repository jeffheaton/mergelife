const assert = require('assert')
const ml = require('../mergelife')

describe('MergeLifeRender.randomRule', function () {
  it('should produce expected results in case 1', function () {
    const rule = ml.MergeLifeRender.randomRule()
    assert.equal(/([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})[^a-zA-Z0-9_]?([0-9a-fA-F]{4})/.test(rule), true)
  })
})
