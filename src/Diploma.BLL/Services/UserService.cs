﻿using System;
using System.Data.Entity;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Diploma.BLL.Contracts;
using Diploma.BLL.Contracts.DTO;
using Diploma.BLL.Contracts.Services;
using Diploma.BLL.Properties;
using Diploma.DAL.Contexts;
using Diploma.DAL.Entities;

namespace Diploma.BLL.Services
{
    internal sealed class UserService : IUserService
    {
        private readonly Func<CompanyContext> _companyContextFactory;

        private readonly ICryptoService _cryptoService;

        private readonly IMapper _mapper;

        public UserService(Func<CompanyContext> companyContextFactory, ICryptoService cryptoService, IMapper mapper)
        {
            _companyContextFactory = companyContextFactory;
            _cryptoService = cryptoService;
            _mapper = mapper;
        }

        public async Task<OperationResult<UserDto>> CreateUserAsync(
            CustomerRegistrationDataDto customerRegistrationData,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var customerEntity = _mapper.Map<CustomerEntity>(customerRegistrationData);

                using (var context = _companyContextFactory())
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        var userDb = context.Users.Add(customerEntity);
                        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                        transaction.Commit();

                        var userDto = _mapper.Map<UserDto>(userDb);

                        return OperationResult<UserDto>.CreateSuccess(userDto);
                    }
                    catch (TaskCanceledException)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                return OperationResult<UserDto>.CreateFailure(ex);
            }
        }

        public async Task<OperationResult<UserDto>> GetUserByCredentialsAsync(
            UserAuthorizationDataDto userAuthorizationData,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (var context = _companyContextFactory())
                {
                    var userDb = await context.Users.AsNoTracking()
                        .SingleOrDefaultAsync(UserNameEquals(userAuthorizationData.Username), cancellationToken).ConfigureAwait(false);

                    if (userDb == null)
                    {
                        return OperationResult<UserDto>.CreateFailure(Resources.Exception_Authorization_Username_Not_Found);
                    }

                    if (_cryptoService.VerifyPasswordHash(userAuthorizationData.Password, userDb.PasswordHash))
                    {
                        var userDto = _mapper.Map<UserDto>(userDb);
                        return OperationResult<UserDto>.CreateSuccess(userDto);
                    }

                    return OperationResult<UserDto>.CreateFailure(Resources.Exception_Authorization_Username_Or_Password_Invalid);
                }
            }
            catch (Exception ex)
            {
                return OperationResult<UserDto>.CreateFailure(ex);
            }
        }

        public async Task<OperationResult<bool>> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken)
        {
            try
            {
                using (var context = _companyContextFactory())
                {
                    var isUnique = !await context.Users.AnyAsync(UserNameEquals(username), cancellationToken).ConfigureAwait(false);
                    return OperationResult<bool>.CreateSuccess(isUnique);
                }
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.CreateFailure(ex);
            }
        }

        public async Task<OperationResult<UserDto>> UpdateUserAsync(
            UserUpdateRequestDataDto userUpdateRequestData,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using (var context = _companyContextFactory())
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        var userDb = await context.Users.SingleAsync(x => x.Id == userUpdateRequestData.Id, cancellationToken).ConfigureAwait(false);

                        _mapper.Map(userUpdateRequestData, userDb);

                        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                        transaction.Commit();

                        var userDto = _mapper.Map<UserDto>(userDb);

                        return OperationResult<UserDto>.CreateSuccess(userDto);
                    }
                    catch (TaskCanceledException)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                return OperationResult<UserDto>.CreateFailure(ex);
            }
        }

        [SuppressMessage("ReSharper", "SpecifyStringComparison", Justification = "This must be used as is cuz of LINQ to entities.")]
        private static Expression<Func<UserEntity, bool>> UserNameEquals(string username)
        {
            return x => username.ToUpper() == x.Username.ToUpper();
        }
    }
}
