using System;
using System.Collections.Generic;
using System.Linq;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Data.Data;
using DistributionSystem.Data.Repositories;

namespace DistributionSystem.Business.Services
{
    public class CustomerFullDetailsDto
    {
        public CustomerDto Customer { get; set; }
        public List<SalesInvoiceDto> Invoices { get; set; } = new List<SalesInvoiceDto>();
        public List<InboundOrderDto> Inbounds { get; set; } = new List<InboundOrderDto>();
    }

    public class CustomerDetailsService : BaseService
    {
        private readonly SqlConnectionFactory _factory;

        public CustomerDetailsService()
        {
            _factory = new SqlConnectionFactory();
        }

        public CustomerFullDetailsDto GetCustomerFullDetails(int customerId)
        {
            return Execute(() =>
            {
                var dto = new CustomerFullDetailsDto();
                var customerRepo = new CustomerRepository(_factory);

                var c = customerRepo.GetById(customerId);
                if (c == null) return null;

                dto.Customer = new CustomerDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Phone = c.Phone,
                    Address = c.Address,
                    CreatedAt = c.CreatedAt,
                    CustomerType = (CustomerType)c.CustomerType
                };

                if (dto.Customer.CustomerType == CustomerType.Invoices)
                {
                    try
                    {
                        dto.Invoices = new SalesInvoiceService()
                            .GetAllInvoices()
                            ?.Where(i => i.CustomerId == customerId)
                            .ToList() ?? new List<SalesInvoiceDto>();
                    }
                    catch { dto.Invoices = new List<SalesInvoiceDto>(); }
                }
                else // Inbounds
                {
                    try
                    {
                        dto.Inbounds = new InboundService()
                            .GetAllInboundOrders()
                            ?.Where(o => o.CustomerId == customerId)
                            .ToList() ?? new List<InboundOrderDto>();
                    }
                    catch { dto.Inbounds = new List<InboundOrderDto>(); }
                }

                return dto;
            });
        }
    }
}