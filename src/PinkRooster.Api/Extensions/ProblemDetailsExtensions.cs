using Microsoft.AspNetCore.Mvc;

namespace PinkRooster.Api.Extensions;

public static class ProblemDetailsExtensions
{
    public static ObjectResult ProblemBadRequest(this ControllerBase controller, string detail) =>
        controller.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: detail);

    public static ObjectResult ProblemUnauthorized(this ControllerBase controller, string detail) =>
        controller.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: detail);

    public static ObjectResult ProblemForbidden(this ControllerBase controller, string detail) =>
        controller.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: detail);

    public static ObjectResult ProblemConflict(this ControllerBase controller, string detail) =>
        controller.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflict",
            detail: detail);
}
