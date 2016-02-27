(function ($, document, window) {

    function init(page) {

        var url = 'components/tunerproviders/' + page.CurrentInfo.Type + '.js';
        
        require([url], function (factory) {

            var instance = new factory(page, {
            });

            instance.init();
        });
    }
    function getTunerName(providerId) {

        providerId = providerId.toLowerCase();

        switch (providerId) {

            case 'm3u':
                return 'M3U Playlist';
            case 'hdhomerun':
                return 'HDHomerun';
            default:
                return 'Unknown';
        }
    }

    function loadTemplate(page) {

        var xhr = new XMLHttpRequest();
        xhr.open('GET', 'components/tunerproviders/' + page.CurrentInfo.Type + '.html', true);

        xhr.onload = function (e) {

            var html = this.response;
            var elem = page.querySelector('.providerTemplate');
            elem.innerHTML = Globalize.translateDocument(html);
            $(elem).trigger('create');

            init(page);
        }

        xhr.send();
    }

    function BaseSubmitInfo(page) {

        Dashboard.showLoadingMsg();
        page.CurrentInfo.IsEnabled = page.querySelector('.chkEnabled').checked;
        page.CurrentInfo.Url = page.querySelector('.txtDevicePath').value;
        page.CurrentInfo.DataVersion = 1;
        page.CurrentInfo.ChannelMaps = page.querySelector('.txtChannelMaps').value;

        ApiClient.ajax({
            type: "POST",
            url: ApiClient.getUrl('LiveTv/TunerHosts'),
            data: JSON.stringify(page.CurrentInfo),
            contentType: "application/json"

        }).then(function () {

            Dashboard.processServerConfigurationUpdateResult();
            Dashboard.navigate('livetvstatus.html');

        }, function () {
            Dashboard.hideLoadingMsg();
            Dashboard.alert({
                message: Globalize.translate('ErrorSavingTvProvider')
            });
        });

    }
    function BaseReload(page) {
        var wait = $.Deferred();
        page.querySelector('.txtDevicePath').value = '';
        if (page.CurrentInfo.Id) {
            ApiClient.getNamedConfiguration("livetv").then(function (config) {
                page.CurrentInfo = config.TunerHosts.filter(function (i) { return i.Id == page.CurrentInfo.Id; })[0];
                page.querySelector('.txtDevicePath').value = page.CurrentInfo.Url || '';
                page.querySelector('.txtChannelMaps').value = page.CurrentInfo.ChannelMaps || '';
                page.querySelector('.chkEnabled').checked = page.CurrentInfo.IsEnabled;
                wait.resolve(page.CurrentInfo);
            });
        } else {
            page.querySelector('.chkEnabled').checked = true;
            wait.resolve(page.CurrentInfo);
        }
        return wait;
    }

    $(document).on('pageshow', "#liveTvTunerProviderPage", function () {

        Dashboard.showLoadingMsg();
        var page = this;
        page.CurrentInfo = { Id: getParameterByName('id'), Type: getParameterByName('type') }
        page.SubmitInfo = function () { BaseSubmitInfo(page); return false; };
        page.Reload = function () { return BaseReload(page); };
        page.querySelector('.tunerHeader').innerHTML = getTunerName(page.CurrentInfo.Type)+" Setup";
        loadTemplate(page);
    });

})(jQuery, document, window);
