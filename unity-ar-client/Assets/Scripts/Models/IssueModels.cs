using System;
using UnityEngine;

namespace Inspection.Models
{
    [Serializable]
    public class Position
    {
        public float x;
        public float y;
        public float z;

        public Position() { }

        public Position(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Position(Vector3 v)
        {
            this.x = v.x;
            this.y = v.y;
            this.z = v.z;
        }

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    [Serializable]
    public class Issue
    {
        public string id;
        public string title;
        public string description;
        public string priority; // low, medium, high
        public string status;   // open, in_progress, resolved
        public Position position;
        public string createdAt;
        public string updatedAt;

        public Color GetPriorityColor()
        {
            switch (priority)
            {
                case "high":
                    return new Color(0.96f, 0.25f, 0.37f); // Rose red
                case "medium":
                    return new Color(0.96f, 0.62f, 0.04f); // Amber orange
                case "low":
                default:
                    return new Color(0.58f, 0.64f, 0.72f); // Slate gray
            }
        }

        public Color GetStatusColor()
        {
            switch (status)
            {
                case "resolved":
                    return new Color(0.06f, 0.73f, 0.51f); // Emerald green
                case "in_progress":
                    return new Color(0.96f, 0.62f, 0.04f); // Amber
                case "open":
                default:
                    return new Color(0.13f, 0.83f, 0.93f); // Cyan
            }
        }
    }

    [Serializable]
    public class CreateIssueRequest
    {
        public string title;
        public string description;
        public string priority;
        public Position position;

        public string ToJson() => JsonUtility.ToJson(this);
    }

    [Serializable]
    public class UpdateStatusRequest
    {
        public string status;

        public string ToJson() => JsonUtility.ToJson(this);
    }

    [Serializable]
    public class IssueListResponse
    {
        public Issue[] items;
        public int total;
    }

    [Serializable]
    public class HealthResponse
    {
        public string status;
        public string database;
        public int issueCount;
        public int uptimeSec;
        public string time;
    }

    [Serializable]
    public class ApiErrorDetail
    {
        public string code;
        public string message;
        public string field;
    }

    [Serializable]
    public class ApiErrorResponse
    {
        public ApiErrorDetail error;
    }
}
