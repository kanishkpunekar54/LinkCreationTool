// src/Services/CrqServices.js

export const runGtp = async (crqNumber, mode, variants, targetUrl) => {
  try {
    const response = await fetch("http://localhost:5116/api/Crq/run-gtp", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        crqNumber,
        mode,
        variants,
        targetUrl,
      }),
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(errorText || "Failed to start GTP process");
    }

    return await response.json();
  } catch (err) {
    throw new Error(err.message || "Something went wrong");
  }
};