using BarberBook.Web.Models.Inventory;
using FluentValidation;

namespace BarberBook.Web.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU is required.")
            .MaximumLength(50).WithMessage("SKU cannot exceed 50 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(100).WithMessage("Category cannot exceed 100 characters.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(30).WithMessage("Unit cannot exceed 30 characters.");

        RuleFor(x => x.SellPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Sell price cannot be negative.");

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Cost price cannot be negative.");

        RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder level cannot be negative.");

        RuleFor(x => x.InitialStock)
            .GreaterThanOrEqualTo(0).WithMessage("Initial stock cannot be negative.");
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(100).WithMessage("Category cannot exceed 100 characters.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required.")
            .MaximumLength(30).WithMessage("Unit cannot exceed 30 characters.");

        RuleFor(x => x.SellPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Sell price cannot be negative.");

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Cost price cannot be negative.");

        RuleFor(x => x.ReorderLevel)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder level cannot be negative.");
    }
}

public class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .NotEqual(0).WithMessage("Quantity adjustment cannot be zero.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason for manual stock adjustment is mandatory.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}

public class CreateEquipmentRequestValidator : AbstractValidator<CreateEquipmentRequest>
{
    public CreateEquipmentRequestValidator()
    {
        RuleFor(x => x.AssetTag)
            .NotEmpty().WithMessage("Asset tag is required.")
            .MaximumLength(50).WithMessage("Asset tag cannot exceed 50 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Equipment name is required.")
            .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(100).WithMessage("Category cannot exceed 100 characters.");

        RuleFor(x => x.BrandModel)
            .MaximumLength(150).WithMessage("Brand model cannot exceed 150 characters.");

        RuleFor(x => x.SerialNo)
            .MaximumLength(100).WithMessage("Serial number cannot exceed 100 characters.");

        RuleFor(x => x.PurchaseCost)
            .GreaterThanOrEqualTo(0).When(x => x.PurchaseCost.HasValue)
            .WithMessage("Purchase cost cannot be negative.");

        RuleFor(x => x.InspectionIntervalDays)
            .GreaterThan(0).WithMessage("Inspection interval days must be at least 1.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}

public class UpdateEquipmentRequestValidator : AbstractValidator<UpdateEquipmentRequest>
{
    public UpdateEquipmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Equipment name is required.")
            .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(100).WithMessage("Category cannot exceed 100 characters.");

        RuleFor(x => x.BrandModel)
            .MaximumLength(150).WithMessage("Brand model cannot exceed 150 characters.");

        RuleFor(x => x.SerialNo)
            .MaximumLength(100).WithMessage("Serial number cannot exceed 100 characters.");

        RuleFor(x => x.PurchaseCost)
            .GreaterThanOrEqualTo(0).When(x => x.PurchaseCost.HasValue)
            .WithMessage("Purchase cost cannot be negative.");

        RuleFor(x => x.InspectionIntervalDays)
            .GreaterThan(0).WithMessage("Inspection interval days must be at least 1.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}

public class AssignEquipmentRequestValidator : AbstractValidator<AssignEquipmentRequest>
{
    public AssignEquipmentRequestValidator()
    {
        RuleFor(x => x.BarberId)
            .GreaterThan(0).WithMessage("A valid barber must be selected.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.");
    }
}

public class LogEquipmentCheckRequestValidator : AbstractValidator<LogEquipmentCheckRequest>
{
    public LogEquipmentCheckRequestValidator()
    {
        RuleFor(x => x.Condition)
            .IsInEnum().WithMessage("A valid equipment condition is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}

public class CreatePurchaseRequestValidator : AbstractValidator<CreatePurchaseRequest>
{
    public CreatePurchaseRequestValidator()
    {
        RuleFor(x => x.SupplierName)
            .NotEmpty().WithMessage("Supplier name is required.")
            .MaximumLength(200).WithMessage("Supplier name cannot exceed 200 characters.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one purchase item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("Valid product ID required.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantity must be at least 1.");
            item.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0).WithMessage("Unit cost cannot be negative.");
        });

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}

public class CreateProductSaleRequestValidator : AbstractValidator<CreateProductSaleRequest>
{
    public CreateProductSaleRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one product item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("Valid product ID required.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantity must be at least 1.");
        });

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.");
    }
}

