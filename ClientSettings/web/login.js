// This file gets loaded after the DOM is ready so no need to check
(function () {
    var link = document.createElement('link');
    link.href = 'res://localhost/web/login.css';
    link.type = 'text/css';
    link.rel = 'stylesheet';
    document.head.appendChild(link);
})();

(async function () {
    await CefSharp.BindObjectAsync('ue');

    // Promises have to be handled here because CefSharp doesn't let you access the methods of an object returned by a method
    window.ue.environment.getbaseurl = function (name) {
        return new Promise(function (resolve, reject) {
            if (name === 'launcher.epicgames')
                resolve('https://launcher-website-prod07.ol.epicgames.com');
            else if (name === '{accounts.epicgames}')
                resolve('https://accounts.epicgames.com');
            else if (name === '{accounts.launcher.epicgames}')
                resolve('https://accounts.launcher-website-prod07.ol.epicgames.com');
            else
                console.error('Unknown base URL name ' + name);
        });
    };

    window.ue.signinprompt.getremembermeuser = function (name) {
        return new Promise(function (resolve, reject) {
            resolve(null);
        });
    };

    window.ue.signinprompt.isremembermeenabled = function (name) {
        return new Promise(function (resolve, reject) {
            resolve(false);
        });
    };

    window.ue.signinprompt.requestloginaccount = function (name) {
        window.location.href = "https://launcher-website-prod07.ol.epicgames.com//epic-login";
    };
})();