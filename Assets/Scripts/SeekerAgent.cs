using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class SeekerAgent : Agent
{
    [Header("References")]
    [SerializeField] private Transform hiderTransform; // Reference to the CustomAgent's transform

    public float moveSpeed = 2; // Speed of agent movement.
    public float turnSpeed = 300; // Speed of agent rotation.
    [SerializeField] private float fov = 90f;
    [SerializeField] private float foundDistanceThreshold = 2.0f; // Adjust this threshold based on your environment

    private float seekTimer;
    [SerializeField] private bool canSeek = false;

    Rigidbody m_AgentRb;

    public override void Initialize()
    {
        // Initialization
        canSeek = false;
        seekTimer = GameManager.instance.seekTime; // Adjust the seek time based on your requirements
    }

    public override void OnEpisodeBegin()
    {
        m_AgentRb = GetComponent<Rigidbody>();

        m_AgentRb.velocity = new Vector3(-4f, 0f, 4f);
        transform.rotation = Quaternion.Euler(new Vector3(0f, UnityEngine.Random.Range(0, 360)));

        // Reset Seeker agent's position, state, etc.
        canSeek = false;
        seekTimer = GameManager.instance.seekTime;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Add observations for Seeker agent, e.g., position, velocity, etc.
        // 1. Add the Seeker's position and rotation.
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(transform.rotation);

        // 2. Add the CustomAgent's position.
        sensor.AddObservation(hiderTransform.localPosition);

        // 3. Calculate the direction to the CustomAgent.
        Vector3 directionToCustomAgent = hiderTransform.position - transform.position;
        directionToCustomAgent.y = 0f; // Ensure it's horizontal.
        sensor.AddObservation(directionToCustomAgent.normalized);

        // 4. Add a binary observation to indicate if the CustomAgent is found.
        sensor.AddObservation(IsHiderFound() ? 1f : 0f);
    }

    private bool IsHiderFound()
    {
        // Calculate the direction to the CustomAgent
        Vector3 directionToCustomAgent = hiderTransform.position - transform.position;
        directionToCustomAgent.y = 0f; // Ensure it's horizontal

        // Calculate the angle between the Seeker's forward direction and the direction to the CustomAgent
        float angleToCustomAgent = Vector3.Angle(transform.forward, directionToCustomAgent);

        // Check if the CustomAgent is within the FOV and not blocked by obstacles
        if (angleToCustomAgent <= fov * 0.5f)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToCustomAgent, out hit, foundDistanceThreshold))
            {
                if (hit.collider.TryGetComponent<HiderAgent>(out HiderAgent hider))
                {
                    return true;
                }
            }
        }

        return false;
    }

    #region Actions and Movement
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!canSeek)
        {
            // Timer for the Seeker to wait before starting to seek
            seekTimer -= Time.deltaTime;

            if (seekTimer <= 0)
            {
                canSeek = true;
                EndEpisode();
            }
        }
        else
        {
            // Implement Seeker's logic for searching the environment and deciding actions
            // Update Seeker's position, raycasting, etc.
            MoveAgent(actions);

            if (IsHiderFound())
            {
                // The CustomAgent is within the FOV and not blocked by obstacles
                float reward = 1f;
                SetReward(reward);

                // Inform the CustomAgent that it has been found
                hiderTransform.TryGetComponent<HiderAgent>(out HiderAgent agent);
                agent.OnFound();

                EndEpisode();
            }
        }
    }

    public void MoveAgent(ActionBuffers actionBuffers)
    {
        Vector3 dirToGo = Vector3.zero;
        Vector3 rotateDir = Vector3.zero;

        var continuousActions = actionBuffers.ContinuousActions;

        var forward = Mathf.Clamp(continuousActions[0], -1f, 1f);
        var right = Mathf.Clamp(continuousActions[1], -1f, 1f);
        var rotate = Mathf.Clamp(continuousActions[2], -1f, 1f);

        dirToGo = transform.forward * forward;
        dirToGo += transform.right * right;
        rotateDir = -transform.up * rotate;

        // Apply torque for rotation
        m_AgentRb.AddTorque(transform.up * rotate * turnSpeed, ForceMode.Force);

        m_AgentRb.AddForce(dirToGo * moveSpeed, ForceMode.VelocityChange);

        if (m_AgentRb.velocity.sqrMagnitude > 25f) // slow it down
        {
            m_AgentRb.velocity *= 0.95f;
        }
    }

    #endregion

    #region Heuristic Control

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        if (Input.GetKey(KeyCode.D))
        {
            continuousActionsOut[2] = 1;
        }
        if (Input.GetKey(KeyCode.Z))
        {
            continuousActionsOut[0] = 1;
        }
        if (Input.GetKey(KeyCode.Q))
        {
            continuousActionsOut[2] = -1;
        }
        if (Input.GetKey(KeyCode.S))
        {
            continuousActionsOut[0] = -1;
        }
    }

    #endregion

    #region Collision Handling

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.TryGetComponent<Wall>(out Wall wall))
        {
            SetReward(-1f);
            EndEpisode();
        }
    }

    #endregion

}
