define([], function () {

    function loadImage(elem, url, onLoad) {

        var onErrorHandler = function () {
            //addClass(ele, "imageError");
            img.removeEventListener("error", onErrorHandler, false);
            img.removeEventListener("load", onLoadHandler, false);
        };

        var onLoadHandler = function () {

            if (elem.tagName !== "IMG") {
                elem.style.backgroundImage = "url('" + url + "')";
            } else {
                elem.setAttribute("src", url);
            }

            img.removeEventListener("error", onErrorHandler, false);
            img.removeEventListener("load", onLoadHandler, false);
            onLoad(elem);
        };

        var img = new Image();

        if (onLoad) {
            // only register events if callback function is specified
            img.addEventListener("error", onErrorHandler, false);
            img.addEventListener("load", onLoadHandler, false);
        }

        img['src'] = url;

        return Promise.resolve(elem);
    }

    return {
        loadImage: loadImage
    };

});