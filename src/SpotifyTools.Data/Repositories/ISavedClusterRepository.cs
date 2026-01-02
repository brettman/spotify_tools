using SpotifyTools.Data.Repositories.Interfaces;
using SpotifyTools.Domain.Entities;

namespace SpotifyTools.Data.Repositories;

public interface ISavedClusterRepository : IRepository<SavedCluster>
{
    /// <summary>
    /// Gets all saved clusters ordered by creation date
    /// </summary>
    Task<List<SavedCluster>> GetAllOrderedAsync();

    /// <summary>
    /// Gets all finalized clusters ready for playlist generation
    /// </summary>
    Task<List<SavedCluster>> GetFinalizedClustersAsync();

    /// <summary>
    /// Checks if a cluster with the given name already exists
    /// </summary>
    Task<bool> ExistsByNameAsync(string name);
}
