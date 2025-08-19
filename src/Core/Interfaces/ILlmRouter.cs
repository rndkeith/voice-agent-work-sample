using VoiceAgent.Models.LLM;

namespace VoiceAgent.Core.Interfaces;

/// <summary>
/// Provider-agnostic LLM routing interface with intelligent model selection.
/// Optimizes for cost, latency, and quality while maintaining conversation consistency.
/// </summary>
public interface ILlmRouter
{
    /// <summary>
    /// Routes LLM request to optimal provider/model based on conversation context
    /// </summary>
    /// <param name="request">Structured LLM request with conversation context</param>
    /// <param name="routingHints">Optional hints for model selection (complexity, urgency)</param>
    /// <param name="cancellationToken">Request cancellation token</param>
    /// <returns>LLM response with provider metadata and performance metrics</returns>
    Task<LlmResponse> RouteRequestAsync(
        LlmRequest request, 
        RoutingHints? routingHints = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates conversation complexity to determine optimal model tier
    /// </summary>
    /// <param name="conversationContext">Current conversation state and history</param>
    /// <param name="inputComplexity">Assessed complexity of current input</param>
    /// <returns>Recommended model tier and confidence score</returns>
    Task<ModelRecommendation> EvaluateModelRequirementAsync(
        ConversationContext conversationContext, 
        InputComplexity inputComplexity);

    /// <summary>
    /// Monitors provider health and adjusts routing policies
    /// </summary>
    /// <returns>Current provider health status and routing weights</returns>
    Task<ProviderHealthStatus> GetProviderHealthAsync();

    /// <summary>
    /// Retrieves performance metrics for evaluation and optimization
    /// </summary>
    /// <param name="timeRange">Time range for metrics collection</param>
    /// <param name="groupBy">Grouping criteria (provider, model, conversation type)</param>
    /// <returns>Aggregated performance metrics</returns>
    Task<LlmPerformanceMetrics> GetPerformanceMetricsAsync(
        TimeRange timeRange, 
        MetricsGrouping groupBy = MetricsGrouping.Provider);

    /// <summary>
    /// Updates routing configuration for gradual rollouts and A/B testing
    /// </summary>
    /// <param name="configuration">New routing configuration</param>
    /// <param name="rolloutStrategy">Gradual rollout strategy</param>
    /// <returns>Configuration update result</returns>
    Task<ConfigurationUpdateResult> UpdateRoutingConfigurationAsync(
        RoutingConfiguration configuration, 
        RolloutStrategy rolloutStrategy);
}

/// <summary>
/// Model tier recommendations based on conversation complexity and requirements
/// </summary>
public enum ModelTier
{
    /// <summary>Fast, cost-effective model for straightforward interactions</summary>
    Primary,
    
    /// <summary>Balanced model for moderate complexity conversations</summary>
    Enhanced,
    
    /// <summary>Premium model for complex reasoning and edge cases</summary>
    Premium,
    
    /// <summary>Specialized model for specific domain requirements</summary>
    Specialized
}

/// <summary>
/// LLM provider types supported by the routing system
/// </summary>
public enum LlmProvider
{
    /// <summary>Azure OpenAI with HIPAA-compliant configuration</summary>
    AzureOpenAI,
    
    /// <summary>AWS Bedrock with Anthropic Claude models</summary>
    AWSBedrock,
    
    /// <summary>Google Vertex AI with Gemini models</summary>
    GoogleVertex,
    
    /// <summary>Fallback provider for emergency situations</summary>
    Fallback
}

/// <summary>
/// Routing hints for model selection optimization
/// </summary>
public class RoutingHints
{
    /// <summary>Conversation complexity assessment (0.0 - 1.0)</summary>
    public double ComplexityScore { get; set; }
    
    /// <summary>Response urgency requirement</summary>
    public LatencyRequirement LatencyRequirement { get; set; }
    
    /// <summary>Quality requirements for conversation</summary>
    public QualityRequirement QualityRequirement { get; set; }
    
    /// <summary>Cost optimization priority</summary>
    public CostOptimization CostOptimization { get; set; }
}

/// <summary>
/// Latency requirement classifications for routing decisions
/// </summary>
public enum LatencyRequirement
{
    /// <summary>Standard latency acceptable (&lt; 700ms)</summary>
    Standard,
    
    /// <summary>Low latency required (&lt; 400ms)</summary>
    Low,
    
    /// <summary>Ultra-low latency critical (&lt; 200ms)</summary>
    Critical,
    
    /// <summary>Latency budget exhausted, use fastest available</summary>
    Emergency
}

/// <summary>
/// Quality requirement specifications for conversation contexts
/// </summary>
public enum QualityRequirement
{
    /// <summary>Standard quality acceptable for routine interactions</summary>
    Standard,
    
    /// <summary>High quality required for complex slot filling</summary>
    High,
    
    /// <summary>Maximum quality for critical healthcare decisions</summary>
    Maximum,
    
    /// <summary>Specialized quality for domain-specific requirements</summary>
    Specialized
}

/// <summary>
/// Cost optimization strategies for LLM routing
/// </summary>
public enum CostOptimization
{
    /// <summary>Balanced cost and quality optimization</summary>
    Balanced,
    
    /// <summary>Aggressive cost optimization with quality thresholds</summary>
    Aggressive,
    
    /// <summary>Quality-first approach with cost monitoring</summary>
    QualityFirst,
    
    /// <summary>Emergency cost reduction mode</summary>
    Emergency
}
