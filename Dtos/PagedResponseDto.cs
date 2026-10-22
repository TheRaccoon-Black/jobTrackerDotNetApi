namespace JobSeekerNet.Dtos;
//<T> (Generic) artinya bisa dipakai berulang kali
public record  PagedResponseDto<T>
(
     int Page,
     int PageSize,
     int TotalPages,
     int TotalItems,
     IEnumerable<T> Items
);
