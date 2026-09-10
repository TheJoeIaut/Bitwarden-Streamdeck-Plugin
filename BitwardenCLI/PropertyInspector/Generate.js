/**
 * Shows either the password options or the passphrase options, matching the
 * "Type" dropdown. Bitwarden's own generator works the same way.
 */
function updateVisibility() {
    var type = document.getElementById('generatortype');
    var passphrase = type && type.value === 'passphrase';

    setGroupVisible('passwordOption', !passphrase);
    setGroupVisible('passphraseOption', passphrase);
}

function setGroupVisible(className, visible) {
    var elements = document.getElementsByClassName(className);
    for (var i = 0; i < elements.length; i++) {
        elements[i].style.display = visible ? '' : 'none';
    }
}

// sdtools.common.js fills the controls from the saved settings after the
// connection is established, so the first pass has to run once that has happened.
document.addEventListener('DOMContentLoaded', function () {
    updateVisibility();
    // Settings arrive asynchronously; re-apply shortly after so the initial view
    // matches the stored type rather than the markup default.
    setTimeout(updateVisibility, 200);
});
