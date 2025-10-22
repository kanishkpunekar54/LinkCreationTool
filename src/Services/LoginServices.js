// src/services/loginService.js

export const login = async (username, password) => {
  try {
    const response = await fetch("http://localhost:5116/api/Login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ username, password }),
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(errorText || "Login failed");
    }

    return await response.json(); // { Message: "✅ Credentials saved successfully in .env" }
  } catch (err) {
    throw new Error(err.message || "Something went wrong");
  }
};
