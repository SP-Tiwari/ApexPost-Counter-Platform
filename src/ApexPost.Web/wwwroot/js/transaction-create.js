// Adds goods rows to the counter transaction form. Each row posts repeated
// GoodsSku/GoodsQuantity fields, so the server needs no row indexes.
(function () {
    const container = document.getElementById("goods-lines");
    const addButton = document.getElementById("add-goods-line");
    if (!container || !addButton) {
        return;
    }

    addButton.hidden = false;
    addButton.addEventListener("click", function () {
        const rows = container.querySelectorAll(".goods-line");
        const template = rows[rows.length - 1];
        const row = template.cloneNode(true);
        const index = rows.length;

        row.querySelectorAll("select, input").forEach(function (field) {
            field.value = "";
            const label = row.querySelector('label[for="' + field.id + '"]');
            field.id = field.name === "GoodsSku"
                ? "goods-sku-" + index
                : "goods-quantity-" + index;
            if (label) {
                label.htmlFor = field.id;
            }
        });

        container.appendChild(row);
        row.querySelector("select").focus();
    });
})();
