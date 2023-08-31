using Microsoft.Extensions.DependencyInjection;

namespace Lasso.Extensions.DependencyInjection
{
    public class LassoServiceBuilder
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LassoServiceBuilder"/>.
        /// </summary>
        /// <param name="services">The services being configured.</param>
        public LassoServiceBuilder(IServiceCollection services)
            => Services = services;

        /// <summary>
        /// The services being configured.
        /// </summary>
        public virtual IServiceCollection Services { get; }
    }
}
