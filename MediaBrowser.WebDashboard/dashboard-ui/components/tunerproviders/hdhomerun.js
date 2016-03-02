define(['paper-checkbox', 'paper-input'], function () {

    return function (page, options) {

        var self = this;

        function reload() {
            page.querySelector('.chkFavorite').checked = false;
            page.Reload().then(function (info) {
                if (info) { page.querySelector('.chkFavorite').checked = info.ImportFavoritesOnly; }
            });
        }

        function submitForm() {
            page.CurrentInfo.ImportFavoritesOnly = page.querySelector('.chkFavorite').checked;
            page.SubmitInfo();
        }

        self.init = function () {

            options = options || {};

            $('form', page).on('submit', function () {
                submitForm(page);
                return false;
            });

            reload();
            Dashboard.hideLoadingMsg();
        };
    }
});