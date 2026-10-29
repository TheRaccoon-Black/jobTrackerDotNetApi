using System;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace JobSeekerNet.Helpers;


public record ApiResponse<T>(int Status, string Message, T? Data);
public record ApiResponse(int status, string Message);


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
