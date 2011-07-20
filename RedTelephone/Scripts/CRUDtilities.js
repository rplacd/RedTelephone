//The CLIENTSIDE version of generating "fresh" rows in tables - CRUD pages, ticket ntoes - and adding them in.

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
    var table = $("#table");
    var failure = function () { table.children("tbody").children("tr:last").remove(); }
    table.children("tbody").append('<tr class="fresh nodrop nodrag"><td>Attempting to load a new row.</td></tr>');
    var foo = $.ajax({
        url: "/referencedata/" + prefix + "/newrow?ordering=" + EncodeTableMembers(),
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

//The CLIENTSIDE version of drop-down box creation.

//Several utility functions to setup dropdown boxes from JSON.
function addDropdownEmpty(elem, required_p) {
    var option = new Option();
    option.value = "";
    if (required_p) {
        elem.addClass("required");
        elem.attr("value", "");
        option.text = "- (required!)";
    } else {
        elem.removeClass("required");
        option.text = "-";
    }
    elem[0].add(option, null);
}
function setDropdownInvalid(elem, required_p) {
    elem.empty();
    addDropdownEmpty(elem, required_p);
}
function setDropdownArray(elem, arr, required_p) {
    //elem is an array - the "children" member
    elem.empty();
    addDropdownEmpty(elem, required_p);
    if (arr.length < 1) {
        setDropdownInvalid(elem, required_p);
    }
    $.each(arr, function (i, d) {
        var option = new Option();
        option.value = d["code"];
        option.text = d["description"];
        elem[0].add(option, null);
    });
}
//HARDCODES STR_NOT_INSTANTIATED.
//first invalidate all dep + rest dropdowns.
//traverse down topLvltree given the code values of src, update the dep with the child element of the last "parent"
function updateDependentDropdowns(src, topLvlTree, dep, rest, required_p) {
    //first, we empty out dep and rest.
    //if we error out below (if we somehow encounter an invalid path while traversing topLvlTree) the state is then consistent.
    setDropdownInvalid(dep, required_p)
    $.each(rest, function (i, d) {
        setDropdownInvalid(d, required_p);
    });

    var fail = false;
    var traverseState = topLvlTree;
    $.each(src, function (i, srcLevel) {
        if (srcLevel.attr("value") == "") {
            fail = true;
            return;
        }
        //find the object with the code we need
        var curr = $.grep(traverseState, function (child, i, a) {
            return child["code"] == srcLevel.attr("value");
        });
        //then set traverseState to its child member - because we start with an array as well.
        traverseState = curr[0]["children"];
    });
    if (fail)
        return;

    //now set the dependent dropdown.
    setDropdownArray(dep, traverseState, required_p);
}
//REFACTOR: un-copypaste this - describe the above in terms of below.
//like the above, except instead of hardcoding setDropdownArray we have [key, target] pairs which we further traverse by key 
//and then get setdropdown called upon - for when we have more than one dependent and don't just use the "children" key.
function updateDependentDropdownsCustom(src, topLvlTree, key_target_s, rest, required_p) {
    //first, we empty out the deps and rests.
    //if we error out below (if we somehow encounter an invalid path while traversing topLvlTree) the state is then consistent.
    $.each(key_target_s, function (i, d) {
        dep = d[1];
        setDropdownInvalid(dep, required_p);
    });
    $.each(rest, function (i, d) {
        setDropdownInvalid(d, required_p);
    });

    var fail = false;
    var traverseState = topLvlTree;
    var backTrack = undefined;
    $.each(src, function (i, srcLevel) {
        var IELocal = traverseState;
        if (srcLevel.attr("value") == "") {
            fail = true;
            return;
        }
        //find the object with the code we need
        var curr = $.grep(traverseState, (function (child) {
            return child["code"] == srcLevel.attr("value");
        }));
        //while we still need traverseState as a temporary thing, backTrack will link the parent of traverseState - the thing we'd
        //normally call "children" upon.
        backTrack = curr[0];
        traverseState = curr[0]["children"];
    });
    if (fail) {
        return;
    }

    //now set the dependent dropdown.
    $.each(key_target_s, function (i, key_target_pair) {
        key = key_target_pair[0];
        target = key_target_pair[1];
        setDropdownArray(target, backTrack[key], required_p);
    });
}