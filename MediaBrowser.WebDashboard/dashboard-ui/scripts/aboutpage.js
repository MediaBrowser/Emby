﻿(function ($, document) {

    $(document).on('pageshowready', "#aboutPage", function () {

        var page = this;
        
        var elem = $('#appVersionNumber', page);

        elem.html(elem.html().replace('{0}', ConnectionManager.appVersion()));
    });

})(jQuery, document);