// const RULE = 'cb97-6a74-88c0-28aa-1b6a-834b-4fe8-60ac'
const RULE = 'E542-5F79-9341-F31E-6C6B-7F08-8773-7068'
const canvas = document.getElementById('myCanvas')
window.ml = new MergeLifeRender(canvas)
window.ml.init({rule: RULE, canvas: canvas})
window.ml.startAnimation()
