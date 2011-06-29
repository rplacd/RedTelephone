//Setups drag-and-drop editing on a #table, automatically serializing to an #ordering, and a generic add-new-row function.
//It does help in doing CRUD pages.
//implied: a table with id=table, an input with id=ordering (and name=ordering, if that helps)      
function StabilizeState() {
    $("#table").tableDnD({
        onDrop: function (_t, _r) {
            $("#ordering").attr("value", $.tableDnD.serialize());
        }
    });
    $("#ordering").attr("value", EncodeTableMembers());
    $("#form").validate();
}
function EncodeTableMembers() {
    return $("#table").tableDnDSerialize()
}
//encodes the members of the table into JSON array format for shipping over to newrow.
//dealing with state here beats dealing with the DB.
$(document).ready(function () {
    StabilizeState();
});
function AddRow(prefix) {
    table = $("#table");
    var failure = function () { table.children("tbody").children("tr:last").remove(); }
    table.children("tbody").append('<tr class="fresh nodrop nodrag"><td>Attempting to load a new row.</td></tr>');
    var foo = $.ajax({
        url: "./" + prefix + "/newrow?ordering=" + EncodeTableMembers(),
        dataType: "html",
        timeout: 5000,
        success: function (data) {
            table.children("tbody").children("tr:last").remove();
            table.children("tbody").children("tr:last").after(data);
            StabilizeState();
        },
        error: function (data, textStatus) {
            if (textStatus == "timeout") {
                table.children("tbody").children("tr:last").remove();
                table.children("tbody").append('<tr class="fresh nodrop nodrag"><td>Run right out of fresh IDs!</td></tr>');
            } else {
                alert(textstatus);
                failure();
            }
        }
    });
}
function RemoveRow(sel) {
    $("#table").find(sel).remove();
}