/**
 * The item picker is a search box backed by Awesomplete, so a large vault can be filtered
 * by typing. A native <datalist> was tried first and rejected: the browser owns that popup
 * and clips long entries, and entry labels carry the username, so they are long.
 *
 * The input's id matches the "itemname" setting, so sdtools.common.js restores and saves it
 * with no help from here. What it cannot do is fill a suggestion list, so loadConfiguration
 * is wrapped to do that with the item list the payload already carries.
 */
var itemPicker = null;

if (typeof loadConfiguration === 'function') {
    var sdtoolsLoadConfiguration = loadConfiguration;

    loadConfiguration = function (payload) {
        try {
            sdtoolsLoadConfiguration(payload);
        } finally {
            // Runs even if the framework choked on a key, so the picker still fills.
            setItemCandidates(payload ? payload.items : null);
        }
    };
}

document.addEventListener('DOMContentLoaded', function () {
    createItemPicker();
});

function createItemPicker() {
    var input = document.getElementById('itemname');

    if (!input || typeof Awesomplete !== 'function' || itemPicker) {
        return;
    }

    itemPicker = new Awesomplete(input, {
        // Substring matching, so an entry can be found by its username as well as its name.
        filter: Awesomplete.FILTER_CONTAINS,
        // Awesomplete sorts by length by default, which buries the entry you want under
        // whichever ones happen to have the shortest names. Keep the vault's own order.
        sort: false,
        minChars: 0,
        maxItems: 200,
        autoFirst: true
    });

    // Show the whole list on focus, so the picker is still browsable without typing -
    // which is what the old dropdown was good at.
    input.addEventListener('focus', function () {
        if (itemPicker && itemPicker._list && itemPicker._list.length) {
            itemPicker.evaluate();
            itemPicker.open();
        }
    });

    // Picking a suggestion does not raise 'input', so save explicitly.
    input.addEventListener('awesomplete-selectcomplete', function () {
        setSettings();
    });
}

function setItemCandidates(items) {
    createItemPicker();

    if (!itemPicker) {
        return;
    }

    var labels = [];

    if (items && items.length) {
        for (var i = 0; i < items.length; i++) {
            var name = items[i] ? items[i].name : null;
            if (name) {
                // The label already carries the username in parentheses; the plugin maps it
                // back to the entry's id when the action runs.
                labels.push(name);
            }
        }
    }

    itemPicker._list = labels;
    itemPicker.list = labels;
}
