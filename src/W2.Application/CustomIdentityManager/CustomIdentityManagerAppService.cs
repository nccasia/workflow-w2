using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace W2.CustomIdentityManager
{
    [Authorize]
    public class CustomIdentityManagerAppService: W2AppService, ICustomIdentityManagerAppService
    {
        private readonly IIdentityUserRepository _userRepository;
        private readonly IdentityUserManager _userManager;

        public CustomIdentityManagerAppService(
                IIdentityUserRepository userRepository,
                IdentityUserManager userManager
            )
        {
            _userRepository = userRepository;
            _userManager = userManager;
        }

        public async Task<PagedResultDto<CustomUserManageDto>> GetListAsync(ListUsersInput input)
        {
            List<IdentityUser> users;

            if (!string.IsNullOrWhiteSpace(input.Roles))
            {
                IList<IdentityUser> temp = await _userManager.GetUsersInRoleAsync(input.Roles);
                users = temp.ToList();
            } else
            {
                users = await _userRepository.GetListAsync();
            }

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                string emailRequest = input.Filter.ToLower().Trim();
                users = users.Where(x => x.UserName.Contains(emailRequest)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(input.Sorting))
            {
                users = ApplySorting(users, input.Sorting);
            }

            var userDtos = users.Select(MapUserToDto)
                                .Skip(input.SkipCount)
                                .Take(input.MaxResultCount)
                                .ToList();

            return new PagedResultDto<CustomUserManageDto>(users.Count(), userDtos);
        }

        private CustomUserManageDto MapUserToDto(IdentityUser user)
        {
            return new CustomUserManageDto
            {
                TenantId = user.TenantId,
                UserName = user.UserName,
                Name = user.Name,
                Surname = user.Surname,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                PhoneNumber = user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                IsActive = user.IsActive,
                LockoutEnabled = user.LockoutEnabled,
                LockoutEnd = user.LockoutEnd,
                ConcurrencyStamp = user.ConcurrencyStamp,
                IsDeleted = user.IsDeleted,
                DeleterId = user.DeleterId,
                DeletionTime = user.DeletionTime,
                CreationTime = user.CreationTime,
                CreatorId = user.CreatorId,
                Id = user.Id,
                ExtraProperties = user.ExtraProperties
            };
        }

        private List<IdentityUser> ApplySorting(List<IdentityUser> users, string sorting)
        {
            if (string.IsNullOrEmpty(sorting))
            {
                return users.OrderBy(u => u.CreationTime).ToList(); // Default sorting
            }

            var sortingParts = sorting.Trim().Split(' ');

            if (sortingParts.Length != 2)
            {
                // Handle invalid sorting format
                return users.OrderBy(u => u.CreationTime).ToList(); // Default sorting
            }

            var property = sortingParts[0].ToLower();
            var direction = sortingParts[1].ToLower();

            if (!string.IsNullOrEmpty(direction) && direction == "asc")
            {
                return users.OrderBy(CreateSortingExpression(property)).ToList();
            }

            return users.OrderByDescending(CreateSortingExpression(property)).ToList();
        }

        private Func<IdentityUser, object> CreateSortingExpression(string property)
        {
            switch (property)
            {
                case "createdat":
                    return u => u.CreationTime;
                case "username":
                    return u => u.UserName;
                case "email":
                    return u => u.Email;
                case "phonenumber":
                    return u => u.PhoneNumber;
                default:
                    return u => u.CreationTime;
            }
        }
    }
}
