using System;
using HCS.Auditing;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HCS.EntityFrameworkCore.Auditing;

public sealed class EfCoreAuditRecordProjectionRepository(
    IDbContextProvider<HCSDbContext> dbContextProvider) :
    EfCoreRepository<HCSDbContext, AuditRecordProjection, Guid>(dbContextProvider),
    IAuditRecordProjectionRepository;
