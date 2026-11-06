using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Errors;

public static class ApplicationErrors
{
    public static Error WorkOrderOutsideOperatingHour(DateTimeOffset startAtUtc, DateTimeOffset endAtUtc) =>
    Error.Conflict(
        "ApplicationErrors.WorkOrder.Outside.OperatingHours",
        $"The WorkOrder time ({startAtUtc} ? {endAtUtc}) is outside of store operating hours.");

    public static Error WorkOrderNotFound =>
    Error.NotFound(
        "ApplicationErrors.WorkOrder.NotFound",
        "WorkOrder dose not exist.");

    public static Error LaborOccupied =>
    Error.Conflict(
        "Employee.LaborOccupied",
        "Labor is already occupied during the requested time.");

    public static Error CustomerNotFound =>
    Error.NotFound(
        "ApplicationErrors.Customer.NotFound",
        "Customer does not exist.");

    public static Error VehicleNotFound =>
    Error.NotFound(
        "ApplicationErrors.Vehicle.NotFound",
        "Vehicle does not exist.");

    public static Error VehicleSchedulingConflict =>
    Error.Conflict(
        "Vehicle_Overlapping_WorkOrder",
        "The vehicle already has an overlapping WorkOrder.");

    public static Error RepairTaskNotFound =>
    Error.NotFound(
        "RepairTask.NotFound",
        "Repair task does not exist.");

    public static Error WorkOrderMustBeCompletedForInvoicing =>
    Error.Conflict(
        "WorkOrder.InvoiceIssuance.InvalidState",
        "WorkOrder must be in 'Completed' state to issue an invoice.");

    public static Error InvoiceNotFound =>
    Error.NotFound(
        "ApplicationErrors.Invoice.NotFound",
        "Invoice does not exist.");

    public static Error InvalidRefreshToken =>
    Error.Validation(
        "RefreshToken.Expiry.Invalid",
        "Expiry must be in the future.");

    public static Error LaborNotFound =>
    Error.NotFound(
        "Employee.LaborNotFound",
        "Labor does not exist.");

    public static Error InvoicePdfGenerationFailed =>
    Error.Failure(
        "ApplicationErrors.Invoice.PdfGenerationFailed",
        "An error occurred while generating the invoice PDF.");

    
    public static readonly Error ExpiredAccessTokenInvalid =
        Error.Unauthorized("Auth_AccessToken_Invalid", "Invalid access token.");

    public static readonly Error UserIdClaimInvalid =
        Error.Unauthorized("Auth_UserIdClaim_Invalid", "Invalid access token.");

    public static readonly Error RefreshTokenExpired =
        Error.Unauthorized("Auth_RefreshToken_Invalid", "Invalid or expired refresh token.");


    public static readonly Error UserNotFound =
    Error.NotFound(
        code: "Auth.User.NotFound",
        description: "User not found.");

    public static readonly Error TokenGenerationFailed =
    Error.Failure(
        code: "Auth.TokenGeneration.Failed",
        description: "Failed to generate new JWT token.");

    public static readonly Error InvoiceAlreadyIssued =
        Error.Conflict("Invoice_Already_Issued", "An invoice has already been issued for this work order.");
}