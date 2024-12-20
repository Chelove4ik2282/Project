﻿using Proj.Context;
using Proj.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Proj.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;
        private readonly IWebHostEnvironment _env;

        public UserService(AppDbContext context, ITokenService tokenService, IWebHostEnvironment env)
        {
            _context = context;
            _tokenService = tokenService;
            _env = env;
        }

        public async Task<AuthResponseDTO> AuthenticateAsync(LoginDTO model)
        {
            var user = _context.Users.SingleOrDefault(u => u.Username == model.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
            {
                return null; // Неверные учетные данные
            }

            // Создаем ClaimsIdentity на основе данных пользователя
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Status)
        };

            var identity = new ClaimsIdentity(claims, "Token");

            // Генерация Access Token с использованием ClaimsIdentity
            var accessToken = _tokenService.GenerateAccessToken(identity);
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Сохранение Refresh Token в базе данных
            user.RefreshToken = refreshToken; // Обновите свойство RefreshToken в модели User
            await _context.SaveChangesAsync();

            return new AuthResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Id = user.Id,
                Username = user.Username,
                Role = user.Status
            };

        }
        public User ValidateUser(string username, string password)
        { 
                var user = _context.Users.SingleOrDefault(u => u.Username == username);
                if (user == null)
                {
                    Console.WriteLine($"User not found: {username}");
                    return null; // Неверные учетные данные
                }

                if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
                {
                    Console.WriteLine($"Password mismatch for user: {username}"); 
                return null; // Неверные учетные данные

                }

                return user; // Успешная аутентификация
 
        }

        public async Task CreateUser(User newUser)
        {
            if (UserExists(newUser.Username))
            {
                throw new Exception("User with this username already exists.");
            }
            if (string.IsNullOrEmpty(newUser.Department))
            {
                throw new ArgumentException("Department cannot be null or empty.");
            }

            newUser.RefreshToken = _tokenService.GenerateRefreshToken(); // Генерация refresh token

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync(); // Сохраняем пользователя
        }


        public async Task<IEnumerable<User>> GetAllUsers()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<User> GetUserById(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                throw new Exception($"User with ID {id} not found.");
            }
            return user;
        }

        public async Task UpdateUserStatus(int userId, string newStatus)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new Exception($"User with ID {userId} not found.");
            }

            if (newStatus != "admin" && newStatus != "manager" && newStatus != "worker")
            {
                throw new Exception("Invalid status provided.");
            }

            user.Status = newStatus;
            await _context.SaveChangesAsync();
        }

        public async Task AssignTaskToUser(int userId, int taskId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new Exception($"User with ID {userId} not found.");
            }

            var taskExists = await _context.Tasks.AnyAsync(t => t.Id == taskId);
            if (!taskExists)
            {
                throw new Exception($"Task with ID {taskId} not found.");
            }

            if (!user.TaskIds.Contains(taskId))
            {
                user.TaskIds.Add(taskId);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                throw new Exception($"User with ID {id} not found.");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        public bool UserExists(string username)
        {
            return _context.Users.Any(u => u.Username == username);
        }

        public void AddRefreshToken(string username, string refreshToken)
        {
            var user = _context.Users.SingleOrDefault(u => u.Username == username);
            if (user == null)
            {
                throw new Exception("User not found.");
            }

            user.RefreshToken = refreshToken;
            _context.SaveChanges();
        }

        public object GenerateTokensForUser(User user)
        {
            // Создаем ClaimsIdentity на основе пользователя
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Status) // Добавляем роль пользователя
            };

            var identity = new ClaimsIdentity(claims);

            // Генерация токена доступа
            var accessToken = _tokenService.GenerateAccessToken(identity);
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Сохраняем refresh токен в модели пользователя
            user.RefreshToken = refreshToken;

            // Сохраняем обновленного пользователя в базе данных
            UpdateUser(user);

            return new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };
        }

        private void UpdateUser(User user)
        {
            // Логика для обновления пользователя в базе данных
            _context.Users.Update(user);
            _context.SaveChanges();
        }

        public User GetUserByRefreshToken(string refreshToken)
        {
            return _context.Users.SingleOrDefault(u => u.RefreshToken == refreshToken);
        }

        public async Task<(string FirstName, string LastName)> GetUserNameById(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                throw new Exception($"User with ID {id} not found.");
            }
            return (user.FirstName, user.LastName);
        }


        // Proj.Services/UserService.cs
        public async Task UpdateUser(int id, UpdateUserDTO updateUserDTO)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                throw new Exception($"User with ID {id} not found.");
            }

            // Проверяем текущий пароль, если пользователь хочет изменить его
            if (!string.IsNullOrEmpty(updateUserDTO.CurrentPassword) && !string.IsNullOrEmpty(updateUserDTO.NewPassword))
            {
                if (!BCrypt.Net.BCrypt.Verify(updateUserDTO.CurrentPassword, user.Password))
                {
                    throw new Exception("Current password is incorrect.");
                }

                // Хешируем и обновляем новый пароль
                user.Password = BCrypt.Net.BCrypt.HashPassword(updateUserDTO.NewPassword);
            }

            // Обновляем другие свойства
            user.Username = updateUserDTO.Username ?? user.Username;
            user.FirstName = updateUserDTO.FirstName ?? user.FirstName;
            user.LastName = updateUserDTO.LastName ?? user.LastName;
            user.BirthDate = updateUserDTO.BirthDate != default ? updateUserDTO.BirthDate : user.BirthDate;
            user.HireDate = updateUserDTO.HireDate != default ? updateUserDTO.HireDate : user.HireDate;
            user.Department = updateUserDTO.Department ?? user.Department;
            user.Status = updateUserDTO.Status ?? user.Status;
            user.TaskIds = updateUserDTO.TaskIds ?? user.TaskIds;
            user.ProfilePicturePath = updateUserDTO.ProfilePicturePath ?? user.ProfilePicturePath;

            //// Обработка файла
            //if (file != null)
            //{
            //    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profile-pictures");
            //    if (!Directory.Exists(uploadsFolder))
            //    {
            //        Directory.CreateDirectory(uploadsFolder);
            //    }

            //    var fileName = $"{id}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            //    var filePath = Path.Combine(uploadsFolder, fileName);

            //    using (var stream = new FileStream(filePath, FileMode.Create))
            //    {
            //        await file.CopyToAsync(stream);
            //    }

            //    // Сохраняем путь к файлу
            //    user.ProfilePicturePath = Path.Combine("uploads", "profile-pictures", fileName);
            //}

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }


        public async Task<byte[]> GetProfilePictureAsync(string filename)
        {
            var filePath = Path.Combine(_env.WebRootPath, "uploads", "profile-pictures", filename);
            if (!System.IO.File.Exists(filePath))
            {
                throw new FileNotFoundException("Profile picture not found.", filename);
            }

            return await System.IO.File.ReadAllBytesAsync(filePath);
        }


        public async Task<string> UploadProfilePicture(int userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("No file uploaded.");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new Exception($"User with ID {userId} not found.");
            }

            // Используем Directory.GetCurrentDirectory() для получения пути
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profile-pictures");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"{userId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Сохраняем путь к файлу в базу данных
            user.ProfilePicturePath = Path.Combine("uploads", "profile-pictures", fileName);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return user.ProfilePicturePath; // Возвращаем путь для ответа
        }




    }
}
