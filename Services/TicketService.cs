using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StarApi.Data;
using StarApi.DTOs.Ticket;
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

        public async Task<(IEnumerable<TicketDto> tickets, int total)> GetTicketsAsync(TicketQueryParamsDto query, Guid currentUserId, bool isAdmin)
        {
            var q = _context.Tickets.AsQueryable();

            if (!isAdmin)
            {
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedToUserId == currentUserId);
            }

            if (query.CreatedByUserId.HasValue)
            {
                q = q.Where(t => t.CreatedByUserId == query.CreatedByUserId.Value);
            }

            if (query.AssignedToUserId.HasValue)
            {
                q = q.Where(t => t.AssignedToUserId == query.AssignedToUserId.Value);
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

            var sortOrder = (query.SortOrder ?? "asc").ToLower() == "desc" ? "desc" : "asc";
            var sortBy = (query.SortBy ?? "createdAt").ToLower();

            q = sortBy switch
            {
                "id" => sortOrder == "asc" ? q.OrderBy(t => t.Id) : q.OrderByDescending(t => t.Id),
                "title" => sortOrder == "asc" ? q.OrderBy(t => t.Title) : q.OrderByDescending(t => t.Title),
                "status" => sortOrder == "asc" ? q.OrderBy(t => t.Status) : q.OrderByDescending(t => t.Status),
                "priority" => sortOrder == "asc" ? q.OrderBy(t => t.Priority) : q.OrderByDescending(t => t.Priority),
                "updatedat" => sortOrder == "asc" ? q.OrderBy(t => t.UpdatedAt) : q.OrderByDescending(t => t.UpdatedAt),
                "duedate" => sortOrder == "asc" ? q.OrderBy(t => t.DueDate) : q.OrderByDescending(t => t.DueDate),
                _ => sortOrder == "asc" ? q.OrderBy(t => t.CreatedAt) : q.OrderByDescending(t => t.CreatedAt)
            };

            var total = await q.CountAsync();
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
                    CreatedByUsername = t.CreatedByUser.Username,
                    CreatedByEmail = t.CreatedByUser.Email,
                    AssignedToUserId = t.AssignedToUserId,
                    AssignedToUsername = t.AssignedToUser != null ? t.AssignedToUser.Username : null,
                    AssignedToEmail = t.AssignedToUser != null ? t.AssignedToUser.Email : null,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    DueDate = t.DueDate
                })
                .ToListAsync();

            return (items, total);
        }

        public async Task<TicketDto?> GetTicketAsync(Guid id, Guid currentUserId, bool isAdmin)
        {
            var t = await _context.Tickets
                .Include(x => x.CreatedByUser)
                .Include(x => x.AssignedToUser)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return null;
            if (!isAdmin && t.CreatedByUserId != currentUserId && t.AssignedToUserId != currentUserId) return null;
            return new TicketDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Status = (t.Status ?? "Open").Trim().ToLower(),
                Priority = (t.Priority ?? "medium").Trim().ToLower(),
                CreatedByUserId = t.CreatedByUserId,
                CreatedByUsername = t.CreatedByUser.Username,
                CreatedByEmail = t.CreatedByUser.Email,
                AssignedToUserId = t.AssignedToUserId,
                AssignedToUsername = t.AssignedToUser != null ? t.AssignedToUser.Username : null,
                AssignedToEmail = t.AssignedToUser != null ? t.AssignedToUser.Email : null,
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
                AssignedToUserId = dto.AssignedToUserId,
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
            if (!isAdmin && t.CreatedByUserId != currentUserId && t.AssignedToUserId != currentUserId) return null;

            if (!string.IsNullOrWhiteSpace(dto.Title) && dto.Title != t.Title) t.Title = dto.Title.Trim();
            if (dto.Description != null && dto.Description != t.Description) t.Description = dto.Description;
            if (!string.IsNullOrWhiteSpace(dto.Status)) t.Status = NormalizeStatus(dto.Status);
            if (!string.IsNullOrWhiteSpace(dto.Priority)) t.Priority = NormalizePriority(dto.Priority);
            if (dto.AssignedToUserId.HasValue) t.AssignedToUserId = dto.AssignedToUserId.Value;
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

        public async Task<IEnumerable<TicketStatusCountDto>> GetStatusCountsAsync(Guid currentUserId, bool isAdmin, Guid? createdByUserId, Guid? assignedToUserId)
        {
            var q = _context.Tickets.AsQueryable();
            if (!isAdmin)
            {
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedToUserId == currentUserId);
            }
            if (createdByUserId.HasValue)
            {
                q = q.Where(t => t.CreatedByUserId == createdByUserId.Value);
            }
            if (assignedToUserId.HasValue)
            {
                q = q.Where(t => t.AssignedToUserId == assignedToUserId.Value);
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
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedToUserId == currentUserId);
            }
            var rel = (relation ?? "created").Trim().ToLower();
            if (rel == "assigned") q = q.Where(t => t.AssignedToUserId == userId);
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
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedToUserId == currentUserId);
            }
            var s = (status ?? "open").Trim().ToLower();
            q = q.Where(t => (t.Status ?? string.Empty).ToLower() == s && t.AssignedToUserId != null);

            var items = await q
                .Include(t => t.AssignedToUser)
                .GroupBy(t => new { t.AssignedToUserId, t.AssignedToUser!.Username, t.AssignedToUser!.Email })
                .Select(g => new UserTicketCountDto
                {
                    UserId = g.Key.AssignedToUserId!.Value,
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
                q = q.Where(t => t.CreatedByUserId == currentUserId || t.AssignedToUserId == currentUserId);
            }
            q = q.Where(t => t.AssignedToUserId != null);

            var rows = await q
                .Include(t => t.AssignedToUser)
                .Select(t => new
                {
                    Status = (t.Status ?? "Open").Trim().ToLower(),
                    UserId = t.AssignedToUserId!.Value,
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
