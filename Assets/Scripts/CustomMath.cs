namespace Assets.Scripts
{
	public struct Vector
	{
		public double x;
		public double y;
		public double z;

		public Vector(double x, double y, double z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public static Vector operator *(Vector a, double b)
			=> new Vector(a.x * b, a.y * b, a.z * b);
	}

	public struct Matrix
	{
		public double R1C1;
		public double R1C2;
		public double R1C3;
		public double R2C1;
		public double R2C2;
		public double R2C3;
		public double R3C1;
		public double R3C2;
		public double R3C3;

		public Matrix(double r1C1, double r1C2, double r1C3, double r2C1, double r2C2, double r2C3, double r3C1, double r3C2, double r3C3)
		{
			R1C1 = r1C1;
			R1C2 = r1C2;
			R1C3 = r1C3;
			R2C1 = r2C1;
			R2C2 = r2C2;
			R2C3 = r2C3;
			R3C1 = r3C1;
			R3C2 = r3C2;
			R3C3 = r3C3;
		}

		public static Vector operator *(Matrix a, Vector b)
			=> new Vector(
				a.R1C1 * b.x + a.R1C2 * b.y + a.R1C3 * b.z,
				a.R2C1 * b.x + a.R2C2 * b.y + a.R2C3 * b.z,
				a.R3C1 * b.x + a.R3C2 * b.y + a.R3C3 * b.z
				);
	}
}
