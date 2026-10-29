window.addEventListener('DOMContentLoaded', event => {
    // Search, sorting and paging for list grids, using Simple-DataTables
    // https://github.com/fiduswriter/Simple-DataTables/wiki

    // The library is loaded from a CDN. If it did not load, leave the plain tables as they are.
    if (typeof simpleDatatables === 'undefined') {
        return;
    }

    // Server grids opt in with CssClass="... js-datatable" and call RenderHeaderInThead() after DataBind() so the
    // header is a real <thead>. A grid showing its empty-data message has no header and is left alone.
    document.querySelectorAll('table.js-datatable').forEach(table => {
        if (!table.tHead || table.tHead.rows.length === 0) {
            return;
        }

        // Action columns hold buttons, so sorting by them is meaningless.
        const unsortable = Array.from(table.tHead.rows[0].cells)
            .map((cell, index) => cell.textContent.trim() === 'Action' ? index : -1)
            .filter(index => index >= 0);

        const dataTable = new simpleDatatables.DataTable(table, {
            perPage: 10,
            perPageSelect: [10, 25, 50],
            columns: unsortable.map(index => ({ select: index, sortable: false }))
        });

        // The library's search box has only a placeholder, which screen readers do not treat as a name.
        const searchBox = dataTable.wrapperDOM.querySelector('input.datatable-input');
        if (searchBox) {
            searchBox.setAttribute('aria-label', 'Search this list');
        }

        // By default the library treats a multi-word search as "any word", so "Room 07" would match every room.
        // multiSearch requires every query to match, so searching each word separately gives "all words".
        // This listener runs first (capture phase) and stops the event so the library's own handler does not
        // overwrite the result.
        dataTable.wrapperDOM.addEventListener('keyup', keyEvent => {
            const input = keyEvent.target;
            if (!(input instanceof HTMLInputElement) || !input.classList.contains('datatable-input')) {
                return;
            }

            keyEvent.stopPropagation();
            const words = input.value.trim().split(/\s+/).filter(word => word.length > 0);
            if (words.length === 0) {
                dataTable.search('');
            } else {
                dataTable.multiSearch(words.map(term => ({ term: term })));
            }
        }, true);
    });
});
