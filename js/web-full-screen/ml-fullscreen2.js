// const RULE = 'cb97-6a74-88c0-28aa-1b6a-834b-4fe8-60ac'

function getUrlParameter (name) {
  const name2 = name.replace(/[\[]/, '\\[').replace(/[\]]/, '\\]')
  const regex = new RegExp('[\\?&]' + name2 + '=([^&#]*)')
  const results = regex.exec(location.search)
  return results === null ? '' : decodeURIComponent(results[1].replace(/\+/g, ' '))
}

function beginML() {
  const canvas = document.getElementById("myCanvas")
  const ctx = canvas.getContext("2d")
  ctx.canvas.width  = window.innerWidth;
  ctx.canvas.height = window.innerHeight;
  const RULE = getUrlParameter('rule')
  const zoom = parseInt(getUrlParameter('size'))
  const controls = getUrlParameter('controls') === 'on'

  if (!window.ml) {
    window.ml = new MergeLifeRender()
    window.ml.init({rule: RULE, canvas: canvas, cellSize: zoom, controls:controls})
    window.ml.startAnimation()
  } else {
    window.ml.init({rule: RULE, canvas: canvas, cellSize: zoom, controls:controls})
  }
}

window.onresize = function() {
  beginML()
}

beginML()
