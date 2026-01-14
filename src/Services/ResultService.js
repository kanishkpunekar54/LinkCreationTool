export const getResults = async (crqNumber, mode) => {
  try {
    if (!crqNumber.toUpperCase().startsWith("CRQ")) {
      crqNumber = "CRQ" + crqNumber;
    }

    const response = await fetch(
      `http://localhost:5116/api/Crq/results?crqNumber=${encodeURIComponent(crqNumber)}&mode=${encodeURIComponent(mode)}`
    );

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(errorText || "Failed to fetch results");
    }

    const data = await response.json();

    if (!data || typeof data.content !== "string") {
      console.error("Unexpected API response:", data);
      throw new Error("API did not return expected file content.");
    }

    // ✅ Backend already split these; just return them.
    return {
      mainContent: data.content,
      validationSummary: data.validationSummary || "",
    };
  } catch (err) {
    throw new Error(err.message || "Something went wrong");
  }
};
