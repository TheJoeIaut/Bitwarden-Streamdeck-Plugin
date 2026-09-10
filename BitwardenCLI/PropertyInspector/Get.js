/**
 * The item picker is an <input> backed by a <datalist>, so a large vault can be searched
 * by typing instead of scrolled through.
 *
 * sdtools.common.js restores and saves the input on its own, because the element's id
 * matches the "itemname" setting. What it has no support for is filling a datalist, so
 * loadConfiguration is wrapped to do that with the item list the payload already carries.
 */
if (typeof loadConfiguration === 'function') {
    var sdtoolsLoadConfiguration = loadConfiguration;

    loadConfiguration = function (payload) {
        try {
            sdtoolsLoadConfiguration(payload);
        } finally {
            // Runs even if the framework choked on a key, so the picker still fills.
            populateItemCandidates(payload ? payload.items : null);
        }
    };
}

function populateItemCandidates(items) {
    var list = document.getElementById('itemCandidates');
    if (!list) {
        return;
    }

    list.innerHTML = '';

    if (!items || !items.length) {
        return;
    }

    for (var i = 0; i < items.length; i++) {
        var name = items[i] ? items[i].name : null;
        if (!name) {
            continue;
        }

        var option = document.createElement('option');
        // The label already carries the username in parentheses; the plugin maps it back
        // to the entry's id when the action runs.
        option.value = name;
        list.appendChild(option);
    }
}
