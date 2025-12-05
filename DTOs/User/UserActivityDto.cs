using System;

namespace StarApi.DTOs.User
{
    public class UserActivityDto
    {
        public int PostsCount { get; set; }
        public int Followers { get; set; }
        public int Following { get; set; }
        public DateTime? LastActiveAt { get; set; }
    }
}
