using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartSales.Validation;

public static class ValidationRules
{
    /// <summary>10-digit Indian mobile number.</summary>
    public const string PhoneRegex = @"^[6-9]\d{9}$";
    public const string PhoneMessage = "Enter a valid phone number (10-digit mobile starting with 6-9).";
    public const string EmailMessage = "Enter a valid email address.";
    public const string NameRegex = @"^[A-Za-z0-9 .,'&()\-]+$";
    public const string NameMessage = "Name contains invalid characters.";
    public const string AmountMessage = "Opportunity Amount must be greater than 0.";
    public const string ProbabilityMessage = "Probability must be between 0 and 100.";
    public const string CloseDateMessage = "Expected Close Date cannot be in the past.";
    public const string FollowUpDateMessage = "Follow-up date cannot be earlier than today.";
}

/// <summary>
/// Rejects dates earlier than today, on the server and (through unobtrusive validation) in the browser.
/// Optionally skipped when another property has one of the given values, e.g. Stage = Won/Lost.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class NotPastDateAttribute : ValidationAttribute, IClientModelValidator
{
    public NotPastDateAttribute(string errorMessage)
    {
        ErrorMessage = errorMessage;
    }

    /// <summary>Name of a property that disables the rule when it holds one of <see cref="IgnoreValues"/>.</summary>
    public string? IgnoreWhenProperty { get; set; }

    public string[] IgnoreValues { get; set; } = Array.Empty<string>();

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not DateTime date) return ValidationResult.Success;

        if (IgnoreWhenProperty != null)
        {
            var other = context.ObjectType.GetProperty(IgnoreWhenProperty)?.GetValue(context.ObjectInstance)?.ToString();
            if (other != null && IgnoreValues.Contains(other)) return ValidationResult.Success;
        }

        return date.Date >= DateTime.Today
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage, new[] { context.MemberName! });
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-notpast", ErrorMessage!);
        if (IgnoreWhenProperty != null)
        {
            context.Attributes.TryAdd("data-val-notpast-ignorefield", IgnoreWhenProperty);
            context.Attributes.TryAdd("data-val-notpast-ignorevalues", string.Join(",", IgnoreValues));
        }
    }
}

/// <summary>Decimal must be strictly greater than a minimum (server + client).</summary>
[AttributeUsage(AttributeTargets.Property)]
public class GreaterThanAttribute : ValidationAttribute, IClientModelValidator
{
    private readonly double _min;

    public GreaterThanAttribute(double min, string errorMessage)
    {
        _min = min;
        ErrorMessage = errorMessage;
    }

    public override bool IsValid(object? value) => value switch
    {
        null => true,
        decimal d => d > (decimal)_min,
        double d => d > _min,
        int i => i > _min,
        _ => false
    };

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-greaterthan", ErrorMessage!);
        context.Attributes.TryAdd("data-val-greaterthan-min", _min.ToString(CultureInfo.InvariantCulture));
    }
}
