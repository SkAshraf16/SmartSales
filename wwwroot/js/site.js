// custom client side rules, server has the same rules in Validation/ValidationRules.cs

// date should not be before today (skip when stage/status is closed)
$.validator.addMethod('notpast', function (val, el, p) {
    if (!val) return true;
    if (p.field) {
        var other = $('[name="' + p.field + '"]').val();
        if (p.values.split(',').indexOf(other) >= 0) return true;
    }
    var today = new Date();
    today.setHours(0, 0, 0, 0);
    return new Date(val + 'T00:00:00') >= today;
});
$.validator.unobtrusive.adapters.add('notpast', ['ignorefield', 'ignorevalues'], function (o) {
    o.rules.notpast = { field: o.params.ignorefield, values: o.params.ignorevalues || '' };
    o.messages.notpast = o.message;
});

// amount should be more than min
$.validator.addMethod('greaterthan', function (val, el, min) {
    if (!val) return true;
    return parseFloat(val) > parseFloat(min);
});
$.validator.unobtrusive.adapters.add('greaterthan', ['min'], function (o) {
    o.rules.greaterthan = o.params.min;
    o.messages.greaterthan = o.message;
});

// re-check date when stage or status is changed
$(document).on('change', '#Stage, #Status', function () {
    $('#ExpectedCloseDate, #FollowUpDate').each(function () {
        if ($(this).val()) $(this).valid();
    });
});

// confirm box before delete and other risky actions
$(document).on('submit', 'form[data-confirm]', function (e) {
    if (!confirm($(this).data('confirm'))) e.preventDefault();
});
