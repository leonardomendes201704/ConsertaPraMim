using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServicePaymentTransactionRepository
{
    Task AddAsync(ServicePaymentTransaction transaction);
    Task<(ServicePaymentTransaction Transaction, bool Created)> AddOrGetByProviderTransactionIdAsync(ServicePaymentTransaction transaction);
    Task<ServicePaymentTransaction?> GetByIdAsync(Guid id);
    Task<ServicePaymentTransaction?> GetByProviderTransactionIdAsync(string providerTransactionId);
    Task<IReadOnlyList<ServicePaymentTransaction>> GetByServiceRequestIdAsync(
        Guid serviceRequestId,
        PaymentTransactionStatus? status = null);
    Task UpdateAsync(ServicePaymentTransaction transaction);
}
