(async function() {
    await CefSharp.BindObjectAsync('ue');

    // Promises have to be handled here because CefSharp doesn't let you access the methods of an object returned by a method
    window.ue.signinprompt.isremembermeenabled = name => new Promise(resolve => resolve(false));
})();