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
        page.CurrentInfo.ListingsProvider = page.querySelector('#selectListing').value;

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
                getAllListings(config.ListingProviders,page).always(function (val) {
                    page.querySelector('#selectListing').value = page.CurrentInfo.ListingsProvider || '';
                    wait.resolve(page.CurrentInfo)
                });
            });
        } else {
            page.querySelector('.chkEnabled').checked = true;
            wait.resolve(page.CurrentInfo);
        }
        return wait;
    }
    function getAllListings(listingsProviders,page) {
        var wait = $.Deferred();
        page.querySelector('#selectListing').innerHTML = '';
        if (!listingsProviders) { wait.resolve(true); return wait;}
        var waits = [];
        for (var index in listingsProviders) {
            var provider = listingsProviders[index];
            console.log("Getting listings for: "+ provider.Id)
            waits.push(refreshListings(provider));
        }
        $.when.apply(null, waits).always(function (val) { wait.resolve(true); });
        return wait;
    }
    function refreshListings(provider,page) {
        var wait = $.Deferred();

        if(!provider.Id || !provider.ZipCode || !provider.Country){return;}

        ApiClient.ajax({
            type: "GET",
            url: ApiClient.getUrl('LiveTv/ListingProviders/Lineups', {
                Id: provider.Id,
                Location: provider.ZipCode,
                Country: provider.Country
            }),
            dataType: 'json'

        }).then(function (result) {
            $('#selectListing', page).append(result.map(function (o) {
                return '<option value="' + provider.Id+"_"+o.Id + '">' + o.Name + '</option>';                
            }));
            wait.resolve(true)           
        }, function (result) { wait.resolve(true); });
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
