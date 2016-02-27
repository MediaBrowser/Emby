define(['paper-checkbox', 'paper-input'], function () {

    return function (page, options) {

        var self = this;

        function reload() {
            page.Reload();            
        }

        function submitForm() {  
            page.SubmitInfo();
        }

        self.init = function () {

            options = options || {};

            $('form', page).on('submit', function () {
                submitForm(page);
                return false;
            });

            $('#btnSelectDevicePath', page).on("click.selectDirectory", function () {

                require(['directorybrowser'], function (directoryBrowser) {

                    var picker = new directoryBrowser();

                    picker.show({
                        path: $('.txtDevicePath', page).val(),
                        includeFiles:true,
                        callback: function (path) {

                            if (path) {
                                $('.txtDevicePath', page).val(path);
                            }
                            picker.close();
                        }
                    });
                });
            });

            reload();
            Dashboard.hideLoadingMsg();
        };
    }
});