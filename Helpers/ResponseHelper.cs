namespace JobSeekerNet.Helpers;


public record ApiResponse<T>(int Status, string Message, T? Data);
public record ApiResponse(int status, string Message);
public record PaginatedMeta(int CurrentPage, int PageSize, int TotalPages, int TotalCount );
public record PaginatedData<T>(IEnumerable<T> Items, PaginatedMeta Meta);


public static class ResponseHelper
{
    public static IResult Success<T>(T data, string message = "Success", int status = 200)
    {
        return Results.Json(new ApiResponse<T>(status, message, data), statusCode: status);

    }

    public static IResult SuccessWithoutData(string message = "Success", int status = 200)
    {
        return Results.Json(new ApiResponse(status, message), statusCode: status);
    }

    public static IResult Error(string message , int status = 400)
    {
        return Results.Json(new ApiResponse(status, message), statusCode: status);
    }
}
