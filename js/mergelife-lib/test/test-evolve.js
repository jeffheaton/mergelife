const assert = require('assert')
const ml = require('../src/mergelife')
const mlev = require('../src/mergelife-evolve')
const fs = require('fs')

function getTracker () {
  const renderer = new ml.MergeLifeRender()
  renderer.init({
    rows: 10,
    cols: 10,
    rule: 'E542-5F79-9341-F31E-6C6B-7F08-8773-7068'
  })

  const config = JSON.parse(fs.readFileSync('./test/resources/config1.json', 'utf8'))
  return new mlev.MergeLifeEvolve(renderer, config.objective)
}

describe('MergeLife evolve', function () {
  it('zeros should allocate multi-dim array', function () {
    const tracker = getTracker()
    const a = tracker.zeros([1, 2, 3])
    assert.equal(1, a.length)
    assert.equal(2, a[0].length)
    assert.equal(3, a[0][0].length)
  })
})
