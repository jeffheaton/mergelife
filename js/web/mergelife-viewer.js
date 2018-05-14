// const RULE = 'cb97-6a74-88c0-28aa-1b6a-834b-4fe8-60ac'
const RULE = 'E542-5F79-9341-F31E-6C6B-7F08-8773-7068'

const textTrans = function (ctx) {
  const TEXT_COLOR_UNREAD = 'rgba(255,255,255,0.4)'
  const TEXT_COLOR_READ = 'rgba(255,255,255,1.0)'
  const TEXT_COLOR_SHADOW = 'rgba(0,0,0,0.8)'
  const TEXT_COLOR_HILIGHT = 'rgba(255,255,0,1.0)'

  const TEXT = ['Will *future *humans one day look', 'back on *human-driven *cars in the', 'same way as using *leeches', 'for many medical conditions?']

  const lineHeight = this.ctx.measureText('M').width
  let textY = (this.ctx.canvas.height / 2 - (lineHeight * TEXT.length * 2) / 2) + lineHeight
  ctx.font = '70px Georgia'

  const currentWord = Math.trunc(Math.max(0, (((Date.now() - window.startedTime) / 300) - 10))) - 1

  let drawingWord = 0
  TEXT.forEach(function (text) {
    const mt = ctx.measureText(text.replace(/\*/g, ''))
    let textX = ctx.canvas.width / 2 - mt.width / 2

    const lst = text.split(' ')
    lst.forEach(function (word) {
      let hlite = false
      if (word[0] === '*') {
        word = word.substring(1)
        hlite = true
      }

      if (drawingWord > currentWord) {
        ctx.fillStyle = TEXT_COLOR_UNREAD
        ctx.fillText(word, textX, textY)
      } else {
        ctx.fillStyle = TEXT_COLOR_SHADOW
        ctx.fillText(word, textX + 2, textY + 2)
        ctx.fillStyle = hlite ? TEXT_COLOR_HILIGHT : TEXT_COLOR_READ
        ctx.fillText(word, textX, textY)
      }
      textX += ctx.measureText(word + ' ').width
      drawingWord += 1
    })
    textY += lineHeight * 2
  })
}

const canvas = document.getElementById('myCanvas')
const ctx = canvas.getContext('2d') 
window.ml = new MergeLifeRender(canvas)
window.ml.init({rule: RULE, canvas: canvas})
window.ml.startAnimation()
