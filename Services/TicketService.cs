using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StarApi.Data;
using StarApi.DTOs.Ticket;
using StarApi.DTOs.User;
using StarApi.Models;
using StarApi.Services.Interfaces;

namespace StarApi.Services
{
    public class TicketService : ITicketService
    {
        private readonly AppDbContext _context;

        public TicketService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserDto>> GetAssignableUsersAsync(Guid currentUserId, bool isAdmin, string? status = null)
        {
            var q = _context.Users.AsQueryable();

            // Always exclude disabled users for assignment
            q = q.Where(u => u.Status != "InActive");

            // Filter by status if provided, otherwise default to "Active"
            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim().ToLower();
                q = q.Where(u => u.Status.ToLower() == s);
            }
            else
            {
                // Default to active users if no status specified
                q = q.Where(u => u.Status == "Active");
            }

            return await q.Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                AvatarUrl = u.AvatarUrl,
                Phone = u.Phone,
                Role = u.Role,
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                LastLoginAt = u.LastLoginAt,
                IsVerified = u.IsVerified
            }).ToListAsync();
        }

        public async Task<IEnumerable<TicketDto>> GetTicketsAsync(TicketQueryParamsDto query, Guid currentUserId, bool isAdmin)
        {
            var q = _context.Tickets.AsQueryable();

            if (!isAdmin)
            {
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedTo == currentUserId);
            }

            if (query.CreatedByUserId.HasValue)
            {
                q = q.Where(t => t.CreatedByUserId == query.CreatedByUserId.Value);
            }

            if (query.AssignedTo.HasValue)
            {
                q = q.Where(t => t.AssignedTo == query.AssignedTo.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var s = query.Status.Trim().ToLower();
                q = q.Where(t => (t.Status ?? string.Empty).ToLower() == s);
            }

            if (!string.IsNullOrWhiteSpace(query.Priority))
            {
                var p = query.Priority.Trim().ToLower();
                q = q.Where(t => (t.Priority ?? string.Empty).ToLower() == p);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim().ToLower();
                q = q.Where(t => (t.Title ?? string.Empty).ToLower().Contains(term) || (t.Description ?? string.Empty).ToLower().Contains(term));
            }

            var items = await q
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssignedToUser)
                .Select(t => new TicketDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Status = (t.Status ?? "Open").Trim().ToLower(),
                    Priority = (t.Priority ?? "medium").Trim().ToLower(),
                    CreatedByUserId = t.CreatedByUserId,
                    AssignedTo = t.AssignedTo,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    DueDate = t.DueDate
                })
                .ToListAsync();

            return items;
        }

        public async Task<TicketDto?> GetTicketAsync(Guid id, Guid currentUserId, bool isAdmin)
        {
            var t = await _context.Tickets
                .Include(x => x.CreatedByUser)
                .Include(x => x.AssignedToUser)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return null;
            if (!isAdmin && t.CreatedByUserId != currentUserId && t.AssignedTo != currentUserId) return null;
            return new TicketDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = (t.Status ?? "Open").Trim().ToLower(),
                Priority = (t.Priority ?? "medium").Trim().ToLower(),
                CreatedByUserId = t.CreatedByUserId,
                AssignedTo = t.AssignedTo,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                DueDate = t.DueDate
            };
        }

        public async Task<TicketDto?> CreateTicketAsync(Guid creatorUserId, CreateTicketDto dto)
        {
            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Description = dto.Description,
                Status = "Open",
                Priority = NormalizePriority(dto.Priority),
                CreatedByUserId = creatorUserId,
                AssignedTo = dto.AssignedTo,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DueDate = dto.DueDate
            };

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();
            return await GetTicketAsync(ticket.Id, creatorUserId, true);
        }

        public async Task<TicketDto?> UpdateTicketAsync(Guid id, Guid currentUserId, bool isAdmin, UpdateTicketDto dto)
        {
            var t = await _context.Tickets.FindAsync(id);
            if (t == null) return null;
            if (!isAdmin && t.CreatedByUserId != currentUserId && t.AssignedTo != currentUserId) return null;

            if (!string.IsNullOrWhiteSpace(dto.Title) && dto.Title != t.Title) t.Title = dto.Title.Trim();
            if (dto.Description != null && dto.Description != t.Description) t.Description = dto.Description;
            if (!string.IsNullOrWhiteSpace(dto.Status)) t.Status = NormalizeStatus(dto.Status);
            if (!string.IsNullOrWhiteSpace(dto.Priority)) t.Priority = NormalizePriority(dto.Priority);
            if (dto.AssignedTo.HasValue) t.AssignedTo = dto.AssignedTo.Value;
            if (dto.DueDate.HasValue) t.DueDate = dto.DueDate.Value;

            t.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return await GetTicketAsync(t.Id, currentUserId, isAdmin);
        }

        public async Task<bool> DeleteTicketAsync(Guid id, Guid currentUserId, bool isAdmin)
        {
            var t = await _context.Tickets.FindAsync(id);
            if (t == null) return false;
            if (!isAdmin && t.CreatedByUserId != currentUserId) return false;
            _context.Tickets.Remove(t);
            var saved = await _context.SaveChangesAsync();
            return saved > 0;
        }

        public async Task<IEnumerable<TicketStatusCountDto>> GetStatusCountsAsync(Guid currentUserId, bool isAdmin, Guid? createdByUserId, Guid? AssignedTo)
        {
            var q = _context.Tickets.AsQueryable();
            if (!isAdmin)
            {
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedTo == currentUserId);
            }
            if (createdByUserId.HasValue)
            {
                q = q.Where(t => t.CreatedByUserId == createdByUserId.Value);
            }
            if (AssignedTo.HasValue)
            {
                q = q.Where(t => t.AssignedTo == AssignedTo.Value);
            }
            var data = await q
                .GroupBy(t => (t.Status ?? "Open").Trim().ToLower())
                .Select(g => new TicketStatusCountDto { Status = g.Key, Count = g.Count() })
                .ToListAsync();
            return data;
        }

        public async Task<int> GetCountByUserAndStatusAsync(Guid currentUserId, bool isAdmin, Guid userId, string relation, string? status)
        {
            var q = _context.Tickets.AsQueryable();
            if (!isAdmin)
            {
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedTo == currentUserId);
            }
            var rel = (relation ?? "created").Trim().ToLower();
            if (rel == "assigned") q = q.Where(t => t.AssignedTo == userId);
            else q = q.Where(t => t.CreatedByUserId == userId);
            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim().ToLower();
                q = q.Where(t => (t.Status ?? string.Empty).ToLower() == s);
            }
            return await q.CountAsync();
        }

        public async Task<IEnumerable<UserTicketCountDto>> GetAssignedUsersByStatusAsync(Guid currentUserId, bool isAdmin, string status)
        {
            var q = _context.Tickets.AsQueryable();
            if (!isAdmin)
            {
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedTo == currentUserId);
            }
            var s = (status ?? "open").Trim().ToLower();
            q = q.Where(t => (t.Status ?? string.Empty).ToLower() == s && t.AssignedTo != null);

            var items = await q
                .Include(t => t.AssignedToUser)
                .GroupBy(t => new { t.AssignedTo, t.AssignedToUser!.Username, t.AssignedToUser!.Email })
                .Select(g => new UserTicketCountDto
                {
                    UserId = g.Key.AssignedTo!.Value,
                    Username = g.Key.Username,
                    Email = g.Key.Email,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return items;
        }

        public async Task<AssignedUsersStatusMatrixDto> GetAssignedUsersStatusMatrixAsync(Guid currentUserId, bool isAdmin)
        {
            var q = _context.Tickets.AsQueryable();
            if (!isAdmin)
            {
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedTo == currentUserId);
            }
            q = q.Where(t => t.AssignedTo != null);

            var rows = await q
                .Include(t => t.AssignedToUser)
                .Select(t => new
                {
                    Status = (t.Status ?? "Open").Trim().ToLower(),
                    UserId = t.AssignedTo!.Value,
                    Username = t.AssignedToUser!.Username,
                    Email = t.AssignedToUser!.Email
                })
                .GroupBy(x => new { x.Status, x.UserId, x.Username, x.Email })
                .Select(g => new
                {
                    g.Key.Status,
                    g.Key.UserId,
                    g.Key.Username,
                    g.Key.Email,
                    Count = g.Count()
                })
                .ToListAsync();

            IEnumerable<UserTicketCountDto> map(string status) => rows
                .Where(r => r.Status == status)
                .Select(r => new UserTicketCountDto
                {
                    UserId = r.UserId,
                    Username = r.Username,
                    Email = r.Email,
                    Count = r.Count
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            return new AssignedUsersStatusMatrixDto
            {
                Open = map("open"),
                In_Progress = map("in_progress"),
                Resolved = map("resolved"),
                Closed = map("closed")
            };
        }

        private static string NormalizePriority(string priority)
        {
            var p = (priority ?? "medium").Trim().ToLower();
            return p switch
            {
                "low" => "Low",
                "high" => "High",
                "urgent" => "Urgent",
                _ => "Medium"
            };
        }

        private static string NormalizeStatus(string status)
        {
            var s = (status ?? "open").Trim().ToLower();
            return s switch
            {
                "in_progress" => "In_Progress",
                "resolved" => "Resolved",
                "closed" => "Closed",
                _ => "Open"
            };
        }
    }
}
