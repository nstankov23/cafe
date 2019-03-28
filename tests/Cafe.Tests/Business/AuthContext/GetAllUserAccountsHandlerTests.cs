﻿using Cafe.Core.AuthContext.Commands;
using Cafe.Core.AuthContext.Queries;
using Cafe.Domain.Entities;
using Cafe.Tests.Customizations;
using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cafe.Tests.Business.AuthContext
{
    public class GetAllUserAccountsHandlerTests : ResetDatabaseLifetime
    {
        private readonly SliceFixture _fixture;

        public GetAllUserAccountsHandlerTests()
        {
            _fixture = new SliceFixture();
        }

        [Theory]
        [CustomizedAutoData]
        public async Task CanGetAllUserAccounts(Register[] registerAccountsCommands)
        {
            // Arrange
            foreach (var command in registerAccountsCommands)
            {
                await _fixture.SendAsync(command);
            }

            var query = new GetAllUserAccounts();

            // Act
            var result = await _fixture.SendAsync(query);

            // Assert
            result.Exists(accounts =>
            {
                return accounts.All(a => registerAccountsCommands.Any(registeredAccount =>
                    a.Id == registeredAccount.Id &&
                    a.FirstName == registeredAccount.FirstName &&
                    a.LastName == registeredAccount.LastName &&
                    a.Email == registeredAccount.Email));
            })
            .ShouldBeTrue();
        }

        [Theory]
        [CustomizedAutoData]
        public async Task CanGetAllUserAccountsAndCheckForManagers(
            Manager managerToAssign,
            Waiter waiterToAssign,
            Register managerAccount,
            Register waiterAccount,
            Register[] registerUnassignedAccountsCommands)
        {
            // Arrange
            await _fixture.ExecuteDbContextAsync(async dbContext =>
            {
                dbContext.Managers.Add(managerToAssign);
                dbContext.Waiters.Add(waiterToAssign);
                await dbContext.SaveChangesAsync();
            });

            foreach (var command in registerUnassignedAccountsCommands)
            {
                await _fixture.SendAsync(command);
            }

            await _fixture.SendAsync(managerAccount);
            await _fixture.SendAsync(waiterAccount);

            await _fixture.SendAsync(new AssignManagerToAccount { AccountId = managerAccount.Id, ManagerId = managerToAssign.Id });
            await _fixture.SendAsync(new AssignWaiterToAccount { AccountId = waiterAccount.Id, WaiterId = waiterToAssign.Id });

            var query = new GetAllUserAccounts();

            // Act
            var result = await _fixture.SendAsync(query);

            // Assert
            result.Exists(accounts =>
            {
                var allUnassignedAccountsAreMappedCorrectly = accounts
                    .All(a => registerUnassignedAccountsCommands.Any(registeredAccount =>
                        a.Id == registeredAccount.Id &&
                        a.FirstName == registeredAccount.FirstName &&
                        a.LastName == registeredAccount.LastName &&
                        a.Email == registeredAccount.Email &&

                        // Very important as we have not assigned any managers or waiters to these accounts
                        a.IsManager == false &&
                        a.ManagerId == null &&
                        a.IsWaiter == false &&
                        a.WaiterId == null));

                var managerAccountResult = accounts
                    .SingleOrDefault(a => a.Id == managerAccount.Id &&
                                          a.IsManager &&
                                          a.ManagerId == managerToAssign.Id &&
                                          a.IsWaiter == false &&
                                          a.WaiterId == null);

                var waiterAccountResult = accounts
                    .SingleOrDefault(a => a.Id == waiterAccount.Id &&
                                          a.IsWaiter &&
                                          a.WaiterId == waiterToAssign.Id &&
                                          a.IsManager == false &&
                                          a.ManagerId == null);

                return allUnassignedAccountsAreMappedCorrectly &&
                    managerAccountResult != null &&
                    waiterAccountResult != null;
            })
            .ShouldBeTrue();
        }
    }
}
