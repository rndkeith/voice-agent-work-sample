workspace "voice-agent-showcase" "Healthcare Voice AI Assistant - Architecture Showcase" {
  model {
        user = person "Patient / Caller" "Calls to book/confirm/cancel appointments." "Patient"
        staff = person "Clinic Staff" "Receives handoffs when needed." "Staff"
    
        vas = softwareSystem "Voice Assistant System" "Orchestrates call flow, guardrails, and scheduler handoff." "System" {
            vas_container_api = container "Voice Orchestrator API" "ASP.NET Core (.NET 8)" "Twilio webhook, dialog state, redaction, LLM & scheduler clients, telemetry" {
                component "TwilioWebhookController" "Signature validation, turn mgmt" "C#"
                component "DialogManager" "Intent + slot filling, policies" "C#"
                component "PromptTemplateManager" "Versioned prompts & flags" "C#"
                component "RedactionFilter" "PHI/PII masking at boundaries" "C#"
                component "LlmRouter" "Model selection/promotion; provider adapters" "C#"
                component "LlmClientAdapters" "Azure/Bedrock/Vertex clients" "C#"
                component "SchedulerClient" "Normalized request + idempotency" "C#"
                component "StateStore" "Per-call state & transcript" "C#"
                component "Telemetry" "OTEL traces/metrics/logs" "C#"
                component "FeatureFlags" "Runtime gating/canaries" "C#"
                component "ConsentManager" "Greeting, disclosure, consent" "C#"
            }
            vas_container_store = container "Transcript/Eval Store" "SQLite + files" "Redacted transcripts and eval outputs"
            vas_container_eval = container "Eval Harness" "CLI/scripts" "Runs scripted dialogs and produces metrics"
        }
    
        twilio = softwareSystem "Telephony Provider" "Ingress for calls, webhooks, media/DTMF." "External"
        
        llm_azure = softwareSystem "Azure OpenAI" "GPT-4o family; HIPAA-eligible via BAA." "External"
        llm_bedrock = softwareSystem "AWS Bedrock / Claude" "Anthropic Claude; HIPAA-eligible." "External"
        llm_vertex = softwareSystem "Google Vertex / Gemini" "Gemini Flash/Live; HIPAA program." "External"
        
        sched = softwareSystem "Scheduler System" "Receives structured appointment requests." "External"
        otel = softwareSystem "Observability Stack" "OTLP collector + tracing/metrics viewer." "External"
    
        user -> twilio "Places call"
        vas -> staff "Escalation / Handoff (when needed)"
    
        twilio -> vas_container_api "Webhook events / media" "HTTPS"
        vas_container_api -> llm_azure "Streaming inference + functions (via LlmRouter)" "HTTPS"
        vas_container_api -> llm_bedrock "Streaming inference + functions (via LlmRouter)" "HTTPS"
        vas_container_api -> llm_vertex "Streaming inference + functions (via LlmRouter)" "HTTPS"
        vas_container_api -> sched "POST normalized request w/ idempotency" "HTTPS"
        vas_container_api -> vas_container_store "Persist redacted transcript & metrics" "SQL/FS"
        vas_container_api -> otel "Traces / metrics / logs" "OTLP"
        vas_container_eval -> vas_container_api "Scripted dialog calls" "HTTP"
    }
    views {
      systemlandscape "SystemLandscape" {
        include *
        autoLayout
      }
      systemContext vas "C1" {
        include *
        autolayout lr
      }
      container vas "C2" {
        include *
        autolayout lr
      }
      component vas_container_api "C3Components" {
        include *
        autolayout lr
      }
      styles {
        element "Patient" { 
            shape person 
            background #f1f8e9 
        }
        element "Staff"   { 
            shape person 
            background #e3f2fd 
        }
        element "System"  { 
            background #ede7f6 
        }
        element "External"{ 
            background #fff3e0 
        }
        element "ASP.NET Core (.NET 8)" { 
            background #e8eaf6 
        }
        element "SQLite + files" { 
            background #fce4ec 
        }
        element "CLI/scripts" { 
            background #f3e5f5 
        }
      }
    }
}