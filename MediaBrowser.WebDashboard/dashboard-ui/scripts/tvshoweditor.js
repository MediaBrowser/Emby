(function ($, document) {

    function getNewTvShowPanel(createIfNeeded) {

        var panel = $('.newTvShowPanel');

        if (createIfNeeded && !panel.length) {
  
            var html = '';

            html += '<div>';
            html += '<div data-role="panel" class="newTvShowPanel" data-position="right" data-display="overlay" data-position-fixed="true" data-theme="b">';
            html += '<form class="newTvShowForm">';

            html += '<h3>' + Globalize.translate('HeaderNewTvShow') + '</h3>';

            html += '<div class="fldSelectTvShowFolder">';
            html += '<br />';
            html += '<label for="selectTvShowFolderToAddTo">' + Globalize.translate('LabelSelectTvShowFolder') + '</label>';
            html += '<select id="selectTvShowFolderToAddTo" data-mini="true"></select>';
            html += '</div>';

            html += '<div class="newTvShowInfo">';
            html += '<br />';

            html += '<div>';
            html += '<label for="txtNewTvShowName">' + Globalize.translate('LabelName') + '</label>';
            html += '<input type="text" id="txtNewTvShowName" required="required" />';
            html += '<div class="fieldDescription">' + Globalize.translate('NewTvShowNameExample') + '</div>';
            html += '</div>';

            html += '<br />';

            // newTvShowInfo
            html += '</div>';

            html += '<br />';
            html += '<p>';
            html += '<button type="submit" data-icon="plus" data-mini="true" data-theme="b">' + Globalize.translate('ButtonSubmit') + '</button>';
            html += '</p>';

            html += '</form>';
            html += '</div>';
            html += '</div>';

            panel = $(html).appendTo(document.body).trigger('create').find('.newTvShowPanel');

            $('#txtNewTvShowName', panel).attr('required', 'required');
            $('#selectTvShowFolderToAddTo', panel).attr('required', 'required');

            $('.newTvShowForm', panel).off('submit', onSubmit).on('submit', onSubmit);
        }
        return panel;
    }

    function showTvShowPanel(items) {

        var panel = getNewTvShowPanel(true).panel('toggle');

        require(['jqmicons']);

        populateTvShowFolders(panel);
    }

    function populateTvShowFolders(panel) {

        var select = $('#selectTvShowFolderToAddTo', panel);

        var options = {

            Recursive: true,
            IncludeItemTypes: "Folder",
            SortBy: "SortName"
        };

        ApiClient.getVirtualFolders().done(function (result) {

            var html = '';

            html += '<option value="">' + Globalize.translate('OptionNewTvShow') + '</option>';

            for (var i = 0, length = result.length; i < length; i++) {

                var virtualFolder = result[i];

                if (virtualFolder.CollectionType == "tvshows") {
                    html += virtualFolder.Locations.map(function (i) {
                        return '<option value="' + i + '">' + i + '</option>';
                    });
                }
            }

            select.html(html).val('').selectmenu('refresh').trigger('change');

        });
    }

    function onSubmit() {
        Dashboard.showLoadingMsg();

        var panel = getNewTvShowPanel(false);

        createTvShow(panel);

        return false;
    }

    pageClassOn('pageinit', "libraryPage", function () {

        var page = this;

        // The button is created dynamically
        $(page).on('click', '.btnNewTvShow', function () {

            TvShowEditor.showPanel([]);
        });
    });

    function redirectToTvShow(id) {

        var context = getParameterByName('context');

        ApiClient.getItem(Dashboard.getCurrentUserId(), id).done(function (item) {

            Dashboard.navigate(LibraryBrowser.getHref(item, context));

        });
    }

    function createTvShow(panel) {

        var url = ApiClient.getUrl("/Library/Series", {

            Name: $('#txtNewTvShowName', panel).val(),
            Location: $('#selectTvShowFolderToAddTo', panel).val(),
        });

        ApiClient.ajax({
            type: "POST",
            url: url,
            dataType: "json"

        }).done(function (result) {

            Dashboard.hideLoadingMsg();

            var id = result.Id;

            panel.panel('toggle');
            redirectToTvShow(id);

        });
    }

    window.TvShowEditor = {

        showPanel: function (items) {
            require(['jqmpanel'], function () {
                showTvShowPanel(items);
            });
        },
    };

})(jQuery, document);