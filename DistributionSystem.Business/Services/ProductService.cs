using System;
using System.Collections.Generic;
using System.Linq;
using DistributionSystem.Business.Dtos;
using DistributionSystem.Data.Data;
using DistributionSystem.Data.Entities;
using DistributionSystem.Data.Interfaces;
using DistributionSystem.Data.Repositories;

namespace DistributionSystem.Business.Services
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    public class DataAccessException : Exception
    {
        public DataAccessException(string message, Exception inner) : base(message, inner) { }
    }

    public class ProductService : BaseService
    {
        private readonly IProductRepository _productRepository;
        private readonly SqlConnectionFactory _connectionFactory;

        public ProductService()
            : this(new ProductRepository(new SqlConnectionFactory()), new SqlConnectionFactory()) { }

        public ProductService(IProductRepository productRepository, SqlConnectionFactory connectionFactory)
        {
            _productRepository = productRepository;
            _connectionFactory = connectionFactory;
        }

        public IEnumerable<ProductDto> GetAll()
            => Execute(() => _productRepository.GetAll().Select(ToDto).ToList());

        public ProductDto GetById(int id)
            => Execute(() => { var p = _productRepository.GetById(id); return p == null ? null : ToDto(p); });

        public int AddProduct(ProductDto dto)
        {
            return Execute(() =>
            {
                Validate(dto);
                try { return _productRepository.Insert(ToEntity(dto)); }
                catch (Exception ex) { LogError(ex); throw new DataAccessException("ÍÏË ÎØÃ ÃËäÇÁ ÅÖÇÝÉ ÇáãäÊÌ.", ex); }
            });
        }

        public bool Update(ProductDto dto)
        {
            return Execute(() =>
            {
                if (dto == null || dto.Id <= 0) throw new ValidationException("ãÚÑøÝ ÇáãäÊÌ ÛíÑ ÕÇáÍ.");
                Validate(dto);
                try { var e = ToEntity(dto); e.Id = dto.Id; return _productRepository.Update(e); }
                catch (Exception ex) { LogError(ex); throw new DataAccessException("ÍÏË ÎØÃ ÃËäÇÁ ÊÍÏíË ÇáãäÊÌ.", ex); }
            });
        }

        public bool Delete(int id)
        {
            return Execute(() =>
            {
                if (id <= 0) throw new ValidationException("ãÚÑøÝ ÇáãäÊÌ ÛíÑ ÕÇáÍ.");
                try { return _productRepository.Delete(id); }
                catch (Exception ex) { LogError(ex); throw new DataAccessException("ÍÏË ÎØÃ ÃËäÇÁ ÍÐÝ ÇáãäÊÌ.", ex); }
            });
        }

        private static void Validate(ProductDto dto)
        {
            if (dto == null) throw new ValidationException("ãÚáæãÇÊ ÇáãäÊÌ ÛíÑ ÕÇáÍÉ.");
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("ÇÓã ÇáãäÊÌ ãØáæÈ.");
            if (dto.PurchasePrice <= 0) throw new ValidationException("ÓÚÑ ÇáÔÑÇÁ íÌÈ Ãä íßæä ÃßÈÑ ãä ÕÝÑ.");
            if (dto.SalePrice <= 0) throw new ValidationException("ÓÚÑ ÇáÈíÚ íÌÈ Ãä íßæä ÃßÈÑ ãä ÕÝÑ.");
        }

        private static ProductDto ToDto(Product p) => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            TireSize = p.TireSize,
            PurchasePrice = p.PurchasePrice,
            SalePrice = p.SalePrice
        };

        private static Product ToEntity(ProductDto d) => new Product
        {
            Name = d.Name,
            TireSize = d.TireSize ?? string.Empty,
            PurchasePrice = d.PurchasePrice,
            SalePrice = d.SalePrice
        };
    }
}