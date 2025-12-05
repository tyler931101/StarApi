using System.Collections.Generic;

namespace StarApi.DTOs.User
{
    public class UserListResponseDto
    {
        public List<UserDto> Users { get; set; } = new List<UserDto>();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
