(function() {
	var findAllViewers = function(node) {
  	var results = [];
  	node = node || document.body;
  	if (node.classList && node.classList.contains("ml-viewer")) {
    	results.push(node);
  	}
		var children = node.childNodes;
		if (children) {
    	for (var i = 0; i < children.length; i++) {
      	var child = children[i];
      	results = results.concat(findAllViewers(child));
    	}
  	}
  	return results;
	};

	document.addEventListener('DOMContentLoaded', function() {
		var lst = findAllViewers();
  	alert(lst.length)
	}, false);
})();
