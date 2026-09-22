/**
 * The item picker is a search box backed by Awesomplete, so a large vault can be filtered
 * by typing. A native <datalist> was tried first and rejected: the browser owns that popup
 * and clips long entries, and entry labels carry the username, so they are long.
 *
 * The input's id matches the "itemname" setting, so sdtools.common.js restores and saves it
 * with no help from here. What it cannot do is fill a suggestion list, so loadConfiguration
 * is wrapped to do that with the item list the payload already carries.
 *
 * Saving replaces the whole settings object with what the sdProperty fields hold, so the
 * item list - which has no field - is dropped as soon as anything is typed. The label left
 * in the search box means nothing without it, which is why the picked entry's id is written
 * into a hidden field of its own as it is picked.
 */
var itemPicker = null;

/** Label -> vault id, rebuilt whenever a loaded item list arrives. Empty until then. */
var itemIdsByLabel = {};

if (typeof loadConfiguration === 'function') {
    var sdtoolsLoadConfiguration = loadConfiguration;

    loadConfiguration = function (payload) {
        var input = document.getElementById('itemname');

        // loadConfiguration assigns straight to element values. When it runs while the
        // search box has focus - a settings echo arriving mid-keystroke - it overwrites
        // what is being typed and drops the caret to the end. Put the box back exactly as
        // the typist left it.
        var typing = !!input && document.activeElement === input;
        var typedValue = typing ? input.value : null;
        var caretStart = typing ? input.selectionStart : 0;
        var caretEnd = typing ? input.selectionEnd : 0;

        try {
            sdtoolsLoadConfiguration(payload);
        } finally {
            if (typing) {
                input.value = typedValue;
                try {
                    input.setSelectionRange(caretStart, caretEnd);
                } catch (err) {
                    // Some input types refuse a selection range; the value still stands.
                }
            }

            // Runs even if the framework choked on a key, so the picker still fills.
            setItemCandidates(payload ? payload.items : null);
        }
    };
}

/**
 * Empties the search box and reopens the full list, so a new search can be started without
 * having to select and delete the current entry by hand.
 */
function clearItemSelection() {
    var input = document.getElementById('itemname');

    if (!input) {
        return;
    }

    input.value = '';

    // Explicitly clearing the selection clears the id with it, list loaded or not.
    var storedId = document.getElementById('itemid');

    if (storedId) {
        storedId.value = '';
    }

    setSettings();

    input.focus();

    if (itemPicker && itemPicker._list && itemPicker._list.length) {
        itemPicker.evaluate();
        itemPicker.open();
    }
}

/**
 * Asks the plugin to fetch the vault. The list is loaded once and filtered here from then
 * on, so typing never reaches the Bitwarden CLI.
 */
function requestItemLoad() {
    var token = document.getElementById('loadtoken');

    if (token) {
        token.value = String(Date.now()) + '-' + Math.random().toString(36).slice(2, 8);
    }

    setSettings();
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
        itemNameChanged();
    });
}

/**
 * Records which entry the search box now names, then saves. Called for both typing and
 * picking a suggestion.
 */
function itemNameChanged() {
    rememberSelectedItemId();
    setSettings();
}

/**
 * Puts the id of the entry the search box names into the hidden field, so the plugin still
 * knows which entry is meant once the item list is gone.
 */
function rememberSelectedItemId() {
    var input = document.getElementById('itemname');
    var storedId = document.getElementById('itemid');

    if (!input || !storedId) {
        return;
    }

    // With no list loaded there is nothing to resolve a label against, and blanking the
    // field would throw away an id an earlier session had already worked out.
    if (!Object.keys(itemIdsByLabel).length) {
        return;
    }

    storedId.value = itemIdsByLabel[input.value.trim()] || '';
}

function setItemCandidates(items) {
    createItemPicker();

    if (!itemPicker) {
        return;
    }

    var labels = [];

    itemIdsByLabel = {};

    if (items && items.length) {
        for (var i = 0; i < items.length; i++) {
            var name = items[i] ? items[i].name : null;
            if (name) {
                // The label already carries the username in parentheses; the id beside it
                // is what the action is given when the key is pressed.
                labels.push(name);
                itemIdsByLabel[name] = items[i].id;
            }
        }
    }

    itemPicker._list = labels;
    itemPicker.list = labels;

    // A list arriving is the first chance to say what a selection made earlier means.
    rememberSelectedItemId();
}
