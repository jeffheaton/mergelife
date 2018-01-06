window.KEY_COLOR = [
  [  0,   0,   0], // Black 0
  [255,   0,   0], // Red 1
  [  0, 255,   0], // Green 2
  [255, 255,   0], // Yellow 3
  [  0,   0, 255], // Blue 4
  [255,   0, 255], // Purple 5
  [  0, 255, 255], // Cyan 6
  [255, 255, 255]  // White 7
]

  $("#startButton").click(function(){
    window.updateEvent = setInterval(animateFunction, 10)
    window.selected = null
    $('#mode').val('Select').prop('selected', true)
    updateButtons()
  })
  $("#stopButton").click(function(){
    clearInterval(window.updateEvent)
    window.updateEvent = null
    updateButtons()
  })
  $("#randomRuleButton").click(function(){
    var str = ""
    for(var i=0;i<8;i++) {
      var r = Math.floor(Math.random() * 0x10000)
      r = r.toString(16)
      while(r.length<4) {
        r = "0" + r
      }
      if(str.length>0) {
        str+="-"
      }
      str+=r
    }
    $("#hexCode").val(str)
    result = parseUpdateRule(str)
    window.update_rule = result
    window.selected = null
    $('#mode').val('Select').prop('selected', true)
    renderTable(result)
    startAnimation()
    updateButtons()
  })
  $("#resetButton").click(function(){
    window.stepCount = 0
    randomLattice(window.lattice[window.current_lattice])
    window.selected = null
    $('#mode').val('Select').prop('selected', true)
    render(0)
    updateButtons()
  })
  $("#singleButton").click(function(){
    if(window.updateEvent===null) {
      singleStep()
      window.selected = null
      $('#mode').val('Select').prop('selected', true)
      updateButtons()
    }
  })
  $("#ruleButton").click(function(){
    var hexCode = $("#hexCode").val()

    result = parseUpdateRule(hexCode)
    window.update_rule = result
    window.selected = null
    $('#mode').val('Select').prop('selected', true)
    renderTable(result)
    startAnimation()
    updateButtons()
  })
  $("#clearButton").click(function(){
    clearSelected()
    render(window.selectedSide)
    updateButtons()
  })
  $("#copyLeftButton").click(function(){
    copy(window.scratchLattice,window.lattice[window.current_lattice])
    render(0)
    render(1)
  })
  $("#copyRightButton").click(function(){
    copy(window.lattice[window.current_lattice],window.scratchLattice)
    render(0)
    render(1)
  })
  $("#pasteButton").click(function(){

  })
  $("#dumpLattice1Button").click(function(){
    var lattice = window.lattice[window.current_lattice]
    $("#dump1").val(btoa(JSON.stringify(lattice)))
  })
  $("#loadLattice1Button").click(function(){
    var js = JSON.parse(atob($("#dump1").val()))
    window.lattice[window.current_lattice] = js
    render(0)
  })
  $("#scratchSwapButton").click(function(){
    var t = window.lattice[window.current_lattice]
    window.lattice[window.current_lattice] = window.scratchLattice
    window.scratchLattice = t
    render(0)
    render(1)

  })
  $("#scratchClearButton").click(function(){
    var lattice = window.lattice[window.current_lattice]
    sourcePixel = lattice[0][0]
    for (var row = 0; row < window.scratchLattice.length; row += 1) {
      for (var col = 0; col < window.scratchLattice[0].length; col += 1) {
        targetPixel = window.scratchLattice[row][col]
        targetPixel[0] = sourcePixel[0]
        targetPixel[1] = sourcePixel[1]
        targetPixel[2] = sourcePixel[2]
      }
    }
    render(1)
  })
  $("#myCanvas").mousedown(function(e){
    selectionMousedown(0,e)
  })
  $("#myCanvas").mousemove(function(e){
    selectionMousemove(0,e)
  })
  $("#myCanvas").mouseup(function(e){
    selectionMouseup(0,e)
  })
  $("#scratchCanvas").mousedown(function(e){
    selectionMousedown(1,e)
  })
  $("#scratchCanvas").mousemove(function(e){
    selectionMousemove(1,e)
  })
  $("#scratchCanvas").mouseup(function(e){
    selectionMouseup(1,e)
  })
  $("#c0").click(function(){
    window.penColor = 0
  })
  $("#c1").click(function(){
    window.penColor = 1
  })
  $("#c2").click(function(){
    window.penColor = 2
  })
  $("#c3").click(function(){
    window.penColor = 3
  })
  $("#c4").click(function(){
    window.penColor = 4
  })
  $("#c5").click(function(){
    window.penColor = 5
  })
  $("#c6").click(function(){
    window.penColor = 6
  })
  $("#c7").click(function(){
    window.penColor = 7
  })


  function selectionMousedown(side,e) {
    var mode = $("#mode").val().toLowerCase()

    if( window.updateEvent === null && mode === "select" ) {
      window.selected = null
      window.selectedSide = side
      window.dragging = [Math.floor(e.offsetX/5),Math.floor(e.offsetY/5),0,0]

      render(side)
    }
  }

  function selectionMousemove(side,e) {
    if(window.dragging !== null && window.updateEvent === null && side==window.selectedSide) {
      var lattice = side==0 ? window.lattice[window.current_lattice] : window.scratchLattice

      window.dragging[2] = Math.floor(e.offsetX/5)
      window.dragging[3] = Math.floor(e.offsetY/5)
      render(side)
      var ctx = side==0 ?  $("#myCanvas")[0].getContext('2d') : $("#scratchCanvas")[0].getContext('2d')
      var w = window.dragging[2] - window.dragging[0]
      var h = window.dragging[3] - window.dragging[1]
      ctx.beginPath()
      ctx.rect(window.dragging[0]*5,window.dragging[1]*5,w*5,h*5)
      ctx.stroke()
      updateButtons()
    }
  }

  function copy(source,target) {
    for (var row = 0; row < source.length; row += 1) {
      for (var col = 0; col < source[row].length; col += 1) {
        var pixelSource = source[row][col]
        var pixelTarget = target[row][col]
        pixelTarget[0] = pixelSource[0]
        pixelTarget[1] = pixelSource[1]
        pixelTarget[2] = pixelSource[2]
      }
    }
  }

  function selectionMouseup(side,e) {
    var mode = $("#mode").val().toLowerCase()

    if(window.dragging!==null && side==window.selectedSide) {
      c1 = Math.min(window.dragging[0],window.dragging[2])
      r1 = Math.min(window.dragging[1],window.dragging[3])
      c2 = Math.max(window.dragging[0],window.dragging[2])
      r2 = Math.max(window.dragging[1],window.dragging[3])

      if( c1==0 && r1==0 ) {
        window.selected = null
      } else {
        window.selected = [c1,r1,c2,r2]
      }
      $('#mode').val('Select').prop('selected', true)
      window.dragging = null
    } else if(mode==="stamp") {
      copySelection(side,e)
      updateButtons()
    } else if(mode==="draw") {
      draw(side,e)
    }
    render(0)
    render(1)
    updateButtons()
  }

  function copySelection(side,e) {
    var lattice_source = window.selectedSide==0 ? window.lattice[window.current_lattice] : window.scratchLattice
    var lattice_target = side==0 ? window.lattice[window.current_lattice] : window.scratchLattice
    var w = window.selected[2] - window.selected[0]
    var h = window.selected[3] - window.selected[1]
    target_col = Math.floor(e.offsetX/5)
    target_row = Math.floor(e.offsetY/5)

    for (var row = 0; row < h; row += 1) {
      for (var col = 0; col < w; col += 1) {
        sourcePixel = lattice_source[window.selected[1]+row][window.selected[0]+col]
        targetPixel = lattice_target[target_row+row][target_col+col]
        targetPixel[0] = sourcePixel[0]
        targetPixel[1] = sourcePixel[1]
        targetPixel[2] = sourcePixel[2]
      }
    }
  }

  function draw(side,e) {
    var lattice_target = side==0 ? window.lattice[window.current_lattice] : window.scratchLattice

    target_col = Math.floor(e.offsetX/5)
    target_row = Math.floor(e.offsetY/5)
    targetPixel = lattice_target[target_row][target_col]
    sourcePixel = window.KEY_COLOR[window.penColor]
    targetPixel[0] = sourcePixel[0]
    targetPixel[1] = sourcePixel[1]
    targetPixel[2] = sourcePixel[2]
  }

  function clearSelected() {
    if(window.selected===null) {
      return
    }

    var lattice = window.selectedSide==0 ? window.lattice[window.current_lattice] : window.scratchLattice


    if( c1>=0 && r1>=0 && c2<lattice[0].length && r2<lattice.length) {

    var sourcePixel = lattice[0][0]
    for (var row = window.selected[1]; row < window.selected[3]; row += 1) {
      for (var col = window.selected[0]; col < window.selected[2]; col += 1) {
        var pixel = lattice[row][col]
        pixel[0] = sourcePixel[0]
        pixel[1] = sourcePixel[1]
        pixel[2] = sourcePixel[2]
      }
    }
  }
}

  function alloc(rows, cols, depth) {
    var result = []

    for (var row = 0; row < rows; row += 1) {
        var temp = []
        for (var col = 0; col < cols; col += 1) {
          if(depth==1) {
            temp[col] = 0
          } else {
            var temp2 = []
            for(var d=0;d<depth;d++) {
              temp2[d] = 0
            }
            temp[col] = temp2
          }
        }
        result[row] = temp;
    }

    return result
};

function parseUpdateRule(hexCode) {
  hexCode = hexCode.replace(/-/g, "");
  output = ""
  result = []
  for(var i=0;i<8;i++) {
    var i2 = i*4
    var range=parseInt(hexCode.substr(i2,2),16)
    var percent=parseInt(hexCode.substr(i2+2,2),16)
    if ((percent & 0x80) > 0) {
      percent = percent - 0x100;
    }
    if(percent>0) {
      percent/=127.0
    } else {
      percent/=128.0
    }
    range*=8
    if(range==2040) {
      range=2048
    }
    result.push([range,percent,i])
  }

  result.sort(function(a, b){
      return a[0] - b[0]
  });
  return result
}

function renderTable(update_rule) {
    output = '<table class="table"><thead><tr><th>Index</th><th>Color</th><th>Range</th><th>Percent</th></tr></thead><tbody>'
    var colors = ['Black','Red','Green','Yellow','Blue','Purple','Cyan','White']
    update_rule.forEach(function(obj) {
      output+='<tr><td>'+obj[2]+'</td>'
      output+='<td>'+colors[obj[2]]+'</td>'
      output+='<td>'+obj[0]+'</td>'
      output+='<td>'+parseInt(100*obj[1])+'</td>'
      output+='</tr>'
    })
    output+='</tbody></table>'
    $("#outputDiv").html(output)
}

function randomLattice(lattice) {
  for (var row = 0; row < lattice.length; row += 1) {
      var line = lattice[row]
      for (var col = 0; col < line.length; col += 1) {
        var pixel = line[col]
        for(var i=0;i<3;i++) {
          pixel[i] = Math.floor(Math.random() * 256)
        }
      }
  }
}

function latticeGray(lattice_in,lattice_out) {
  freq = Array.apply(null, Array(256)).map(Number.prototype.valueOf,0)

  for (var row = 0; row < lattice_in.length; row += 1) {
      var line_in = lattice_in[row]
      var line_out = lattice_out[row]
      for (var col = 0; col < line_in.length; col += 1) {
        pixel = line_in[col]
        sum = 0
        for(var i=0;i<3;i++) {
          sum+=pixel[i]
        }
        var v = Math.floor(sum/3)
        freq[v]+=1
        line_out[col] = v
      }
  }

  var mode = freq.indexOf(Math.max(...freq));

  return mode
}

function sumNeighbor(lattice,row,col,pad) {
  const colTransform = [ 0, 0, -1, 1, -1, 1, 1, -1 ]
  const rowTransform = [ -1, 1, 0, 0, -1, 1, -1, 1 ]

  var sum = 0
  for(var i=0;i<colTransform.length;i++) {
    neighborRow = rowTransform[i] + row
    neighborCol = colTransform[i] + col
    if(neighborRow>=0 && neighborCol>=0 && neighborRow<lattice.length && neighborCol<lattice[0].length) {
      sum+=lattice[neighborRow][neighborCol]
    } else {
      sum+=pad
    }
  }
  return sum
}

function updateStep(lattice,lattice_next,update_rule) {
  var lg = window.magnitude
  mode = latticeGray(lattice,lg)

  for (var row = 0; row < lattice.length; row += 1) {
      line = lattice[row]
      line_next = lattice_next[row]
      for (var col = 0; col < line.length; col += 1) {
        var nc = sumNeighbor(lg,row,col,mode)

        for(var i=0;i<update_rule.length;i++) {
          if( nc < update_rule[i][0] ) {
            var ki = update_rule[i][2]
            var pct = update_rule[i][1]

            if(pct<0) {
              pct = Math.abs(pct)
              ki+=1
              if(ki>=update_rule.length) {
                ki = 0
              }
            }

            for(var j=0;j<3;j++) {
              d = window.KEY_COLOR[ki][j] - lattice[row][col][j]
              d*= pct
              d = Math.floor(d)
              line_next[col][j] = line[col][j] + d
            }
            break
          }
        }
      }
  }
}


function getColor(pixel) {
    return "#" + ((1 << 24) + (pixel[0] << 16) + (pixel[1] << 8) + pixel[2]).toString(16).slice(1);
}



function animateFunction( )
{
  singleStep()
}

function startAnimation() {
  window.stepCount = 0
  randomLattice(window.lattice[window.current_lattice])
  if(window.updateEvent===null) {
    window.updateEvent = setInterval(animateFunction, 10)
  }
  window.selectd = null
  window.dragging = null
  updateButtons()
}

function render(side) {
  var output, row, col, r, g, b, c;
  var pixH = 5
  var pixW = 5
  var ctx = side==0 ?  $("#myCanvas")[0].getContext('2d') : $("#scratchCanvas")[0].getContext('2d')
  var lattice = side==0 ? window.lattice[window.current_lattice] : window.scratchLattice

  ctx.strokeStyle = 'grey';
  for (row = 0; row < 100; row += 1) {
      for (col = 0; col < 100; col += 1) {
          ctx.fillStyle = getColor(lattice[row][col])
          ctx.fillRect(col * pixW, row * pixH, pixW, pixH);
      }
  }

  ctx.strokeStyle = "black";

  if(window.selected!==null && window.selectedSide==side ) {
    var w = window.selected[2] - window.selected[0]
    var h = window.selected[3] - window.selected[1]
    ctx.beginPath()
    ctx.rect(window.selected[0]*5,window.selected[1]*5,w*5,h*5)
    ctx.stroke()
  }
}

function singleStep() {
  window.stepCount+=1
  $("#outputStep").html(window.stepCount)
  updateStep(
    window.lattice[window.current_lattice],
    window.lattice[window.current_lattice==0?1:0],
    window.update_rule)
  window.current_lattice = window.current_lattice==0?1:0
  var lattice = window.lattice[window.current_lattice]
  render(0,lattice)
}

function updateButtons() {
  $( "#startButton" ).prop( "disabled", window.updateEvent!=null || window.update_rule ==null )
  $( "#stopButton" ).prop( "disabled", window.updateEvent==null )
  $( "#singleButton" ).prop( "disabled", window.updateEvent!=null || window.update_rule ==null )
  $( "#resetButton" ).prop( "disabled", window.update_rule ==null )
  $( "#ruleButton" ).prop( "disabled", false )
  $( "#clearButton" ).prop( "disabled", window.selected ==null )
  $( "#copyButton" ).prop( "disabled", window.selected ==null )
  $( "#pasteButton" ).prop( "disabled", true )
}

window.lattice = []
window.lattice[0] = alloc(100,100,3)
window.lattice[1] = alloc(100,100,3)
window.scratchLattice = alloc(100,100,3)
window.magnitude = alloc(100,100,1)
window.current_lattice = 0
window.dragging = null
window.selected = null
window.update_rule = null
window.updateEvent = null
window.penColor = 0
window.stepCount = 0
updateButtons()
randomLattice(window.lattice[0])
randomLattice(window.scratchLattice)
render(0,window.lattice[0])
render(1,window.scratchLattice)
