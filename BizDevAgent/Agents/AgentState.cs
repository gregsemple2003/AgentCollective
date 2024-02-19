﻿using BizDevAgent.DataStore;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Rystem.OpenAi;

namespace BizDevAgent.Agents
{
    [TypeId("AgentShortTermMemory")]
    public interface IAgentShortTermMemory
    {
        string ToJson();
    }

    public abstract class AgentState : JsonAsset
    {
        /// <summary>
        /// Information gained from raw sensory data, e.g. viewing logs, reading debugger output.
        /// </summary>
        [JsonIgnore]
        public List<AgentObservation> Observations { get; set; }

        /// <summary>
        /// Our current planning state, which is specialized according to the task we are performing.
        /// </summary>
        public virtual IAgentShortTermMemory ShortTermMemory { get; set; }

        /// <summary>
        /// The current working stack of goals, from the lowest level to the highest level.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<AgentGoal> Goals
        {
            get
            {
                if (_currentGoal == null) yield break;
                var goal = _currentGoal;
                while (goal != null)
                {
                    yield return goal;
                    goal = goal._parent;
                }
            }
        }

        private AgentGoal _currentGoal; // current position within goal hierarchy

        public AgentState()
        {
            Observations = new List<AgentObservation>();
        }

        public void InsertGoal(AgentGoalSpec childSpec, AgentGoal parent = null, bool forceCurrent = false)
        {
            var child = new AgentGoal(childSpec);

            if (parent == null)
            {
                TryGetGoal(out parent);
            }

            if (parent != null)
            {
                parent.Children.Add(child);
            }

            child.SetParent(parent);

            if (_currentGoal == null || forceCurrent)
            {
                _currentGoal = child;
            }
        }

        public void SetCurrentGoal(AgentGoal goal)
        {            
            _currentGoal = goal;
        }

        public void NextGoal()
        {
            _currentGoal.MarkDone();

            var parent = _currentGoal._parent;
            if (parent != null)
            {
                // Find first child that isn't done on parent
                // If all are done, then next loop will pop this goal
                foreach(var child in parent.Children)
                {
                    if (!child.IsDone())
                    {
                        _currentGoal = child;
                        return;
                    }
                }

                // If we get here, then all children are done, return to parent
                _currentGoal = parent;
            }
        }

        public void MarkGoalDone()
        {
            if (_currentGoal != null)
            {
                _currentGoal.MarkDone();
            }
        }

        public bool HasGoals()
        {
            return _currentGoal != null;
        }

        public bool TryGetGoal(out AgentGoal goal)
        {
            goal = _currentGoal;

            return goal != null;
        }
    }
}
